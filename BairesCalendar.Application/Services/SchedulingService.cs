using BairesCalendar.Application.DTOs;
using BairesCalendar.Application.Interfaces;
using BairesCalendar.Domain;
using BairesCalendar.Domain.Entities;
using BairesCalendar.Infrastructure;
using BairesCalendar.Infrastructure.TimeProvider;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BairesCalendar.Application.Services
{
    public class SchedulingService(ApplicationDbContext dbContext, ITimeProvider timeProvider, ILogger<SchedulingService> logger) : ISchedulingService
    {
        private readonly ApplicationDbContext _dbContext = dbContext;
        private readonly ITimeProvider _timeProvider = timeProvider;
        private readonly ILogger<SchedulingService> _logger = logger;

        public async Task<ScheduleMeetingResponseDTO> ScheduleMeetingAsync(ScheduleMeetingRequestDTO request)
        {
            _logger.LogInformation("Attempting to schedule meeting: {Title}", request.Title);

            var participants = await _dbContext.Users
                .Where(u => request.ParticipantIds.Contains(u.Id))
                .ToListAsync();

            if (participants.Count != request.ParticipantIds.Count)
            {
                var missingIds = request.ParticipantIds.Except(participants.Select(p => p.Id)).ToList();
                _logger.LogWarning("Participants not found: {MissingIds}", string.Join(", ", missingIds));
                return new ScheduleMeetingResponseDTO
                {
                    Success = false,
                    Message = $"One or more participant was not found. Missing IDs: {string.Join(", ", missingIds)}"
                };
            }

            // Determine the timezone for the desired start time.
            // Prioritize RequestingUserTimeZoneId, otherwise default to a sensible UTC offset,
            // or perhaps assume client's local timezone if not explicitly provided and a default isn't set.
            // For simplicity, let's assume if not provided, the StartTime is already UTC.
            // In a real system, you might get this from the requesting user's profile.
            TimeZoneInfo requestTimeZone;
            if (!string.IsNullOrEmpty(request.UserTimeZoneId))
            {
                try
                {
                    requestTimeZone = TimeZoneInfo.FindSystemTimeZoneById(request.UserTimeZoneId);
                }
                catch (TimeZoneNotFoundException)
                {
                    _logger.LogError("RequestingUserTimeZoneId '{TimeZoneId}' not found.", request.UserTimeZoneId);
                    return new ScheduleMeetingResponseDTO
                    {
                        Success = false,
                        Message = $"Invalid timezone ID provided: {request.UserTimeZoneId}"
                    };
                }
            }
            else
            {
                // Default to UTC if no timezone is specified for the request.
                requestTimeZone = TimeZoneInfo.Utc;
                _logger.LogInformation("No requesting user timezone specified. Assuming StartTime is UTC.");
            }

            // Normalize time slot to UTC
            var startTimeUtc = TimeZoneInfo.ConvertTimeToUtc(request.StartTime, requestTimeZone);
            var EndTimeUtc = startTimeUtc.Add(request.Duration);

            _logger.LogInformation("Proposed meeting UTC time: {Start} - {End}", startTimeUtc, EndTimeUtc);

            if (startTimeUtc < _timeProvider.UtcNow)
            {
                _logger.LogWarning("Cannot schedule meeting in the past. Proposed start: {Start}, Current UTC: {Now}", startTimeUtc, _timeProvider.UtcNow);
                return new ScheduleMeetingResponseDTO
                {
                    Success = false,
                    Message = "Cannot schedule a meeting in the past."
                };
            }

            // 2. Conflict Detection
            var allParticipantMeetings = new List<Meeting>();
            foreach (var participant in participants)
            {
                // Load existing meetings for each participant
                // Only load meetings that could potentially overlap with the desired slot.
                // This optimization is crucial for performance with many meetings per user.
                var participantMeetings = await _dbContext.Entry(participant)
                    .Collection(u => u.Meetings)
                    .Query()
                    .Where(m => m.StartTimeUtc < EndTimeUtc && m.EndTimeUtc > startTimeUtc) // Pre-filter in DB
                    .ToListAsync();
                allParticipantMeetings.AddRange(participantMeetings);
            }

            // Deduplicate meetings if a meeting has multiple requested participants
            var uniqueParticipantMeetings = allParticipantMeetings.Distinct().ToList();

            foreach (var existingMeeting in uniqueParticipantMeetings)
            {
                if (existingMeeting.OverlapsWith(startTimeUtc, EndTimeUtc))
                {
                    _logger.LogInformation("Conflict detected with existing meeting: {ExistingMeetingTitle} ({ExistingStart} - {ExistingEnd})",
                        existingMeeting.Title, existingMeeting.StartTimeUtc, existingMeeting.EndTimeUtc);

                    // Conflict found, suggest next available slots
                    var suggestedSlots = await FindNextAvailableSlots(
                        [.. participants.Select(p => p.Id)],
                        EndTimeUtc, // Start searching from just after the desired slot
                        request.Duration,
                        count: 3); // Suggest 3 slots

                    return new ScheduleMeetingResponseDTO
                    {
                        Success = false,
                        Message = "Scheduling failed due to a time conflict. See suggestions.",
                        SuggestedSlots = suggestedSlots
                    };
                }
            }

            // 3. Schedule the Meeting if no conflicts
            var newMeeting = new Meeting(
                request.Title,
                startTimeUtc,
                EndTimeUtc,
                participants
            );

            _dbContext.Meetings.Add(newMeeting);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Meeting '{Title}' scheduled successfully with ID: {MeetingId}", request.Title, newMeeting.Id);

            return new ScheduleMeetingResponseDTO
            {
                Success = true,
                MeetingId = newMeeting.Id,
                Message = "Meeting scheduled successfully."
            };
        }

        private async Task<List<TimeSlot>> FindNextAvailableSlots(List<Guid> participantIds, DateTime searchStartUtc, TimeSpan duration, int count)
        {
            var suggestedSlots = new List<TimeSlot>();
            var currentSearchTime = searchStartUtc;
            int foundCount = 0;

            // Define a reasonable search window to avoid infinite loops
            var searchWindowEnd = _timeProvider.UtcNow.AddDays(30);

            while (foundCount < count && currentSearchTime < searchWindowEnd)
            {
                var proposedEndUtc = currentSearchTime.Add(duration);

                // Fetch relevant meetings for all participants within the current search window
                // This query fetches all meetings involving any of the participants that could potentially overlap
                // with `currentSearchTime` to `proposedEndUtc`.
                var overlappingMeetings = await _dbContext.Meetings
                    .Where(m => m.Participants.Any(p => participantIds.Contains(p.Id)) &&
                                m.StartTimeUtc < proposedEndUtc &&
                                m.EndTimeUtc > currentSearchTime)
                    .OrderBy(m => m.StartTimeUtc) // Order by start time for efficient iteration
                    .ToListAsync();

                bool isSlotFree = true;
                foreach (var existingMeeting in overlappingMeetings)
                {
                    if (existingMeeting.OverlapsWith(currentSearchTime, proposedEndUtc))
                    {
                        isSlotFree = false;
                        // Move `currentSearchTime` past the end of the conflicting meeting
                        currentSearchTime = existingMeeting.EndTimeUtc;
                        break; // Exit inner loop and try the new currentSearchTime
                    }
                }

                if (isSlotFree)
                {
                    // Found a free slot
                    suggestedSlots.Add(new TimeSlot(currentSearchTime, proposedEndUtc));
                    foundCount++;
                    currentSearchTime = proposedEndUtc; // Move to the end of the just-found slot for the next search
                }
                else
                {
                    // If a conflict was found and currentSearchTime was updated, the loop continues.
                    // If no conflict was found with fetched meetings but `isSlotFree` is false
                    // (this shouldn't happen with the logic above, but as a safeguard),
                    // or if the loop continued without finding a conflict,
                    // advance by a small increment to avoid getting stuck.
                    if (currentSearchTime == proposedEndUtc) // Prevent infinite loop if no progress is made
                    {
                        currentSearchTime = currentSearchTime.Add(TimeSpan.FromMinutes(15)); // Try next 15 min block
                    }
                }
            }

            return suggestedSlots;
        }
    }
}