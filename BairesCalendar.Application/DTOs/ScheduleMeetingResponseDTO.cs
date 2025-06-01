using BairesCalendar.Domain;

namespace BairesCalendar.Application.DTOs
{
    public class ScheduleMeetingResponseDTO
    {
        public bool Success { get; set; }
        public Guid? MeetingId { get; set; }
        public required string Message { get; set; }
        public List<TimeSlot> SuggestedSlots { get; set; } = [];
    }
}