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
            TimeZoneInfo requestTimeZone;
            if (!string.IsNullOrEmpty(request.UserTimeZoneId))
            {
                try
                {
                    requestTimeZone = TimeZoneInfo.FindSystemTimeZoneById(request.UserTimeZoneId);
                }
                catch (TimeZoneNotFoundException)
                {
                    _logger.LogError("UserTimeZoneId '{TimeZoneId}' not found.", request.UserTimeZoneId);
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

            // Conflict detection
            var allParticipantMeetings = new List<Meeting>();
            foreach (var participant in participants)
            {
                // Load existing meetings for each participant
                // Only load meetings that could potentially overlap with the desired slot.
                var participantMeetings = await _dbContext.Entry(participant)
                    .Collection(u => u.Meetings)
                    .Query()
                    .Where(m => m.StartTimeUtc < EndTimeUtc && m.EndTimeUtc > startTimeUtc) // Pre-filter in DB
                    .ToListAsync();
                allParticipantMeetings.AddRange(participantMeetings);
            }

            foreach (var existingMeeting in allParticipantMeetings.Distinct().ToList())
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

            // Schedule meeting if no conflicts
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
                        currentSearchTime = existingMeeting.EndTimeUtc;
                        break;
                    }
                }

                if (isSlotFree)
                {
                    // Found a free slot
                    suggestedSlots.Add(new TimeSlot(currentSearchTime, proposedEndUtc));
                    foundCount++;
                    currentSearchTime = proposedEndUtc;
                }
                else
                {
                    if (currentSearchTime == proposedEndUtc)
                    {
                        // Try next 15 min block
                        currentSearchTime = currentSearchTime.Add(TimeSpan.FromMinutes(15));
                    }
                }
            }

            return suggestedSlots;
        }
    }
}