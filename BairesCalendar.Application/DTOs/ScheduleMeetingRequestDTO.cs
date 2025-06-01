using System.ComponentModel.DataAnnotations;

namespace BairesCalendar.Application.DTOs
{
    public class ScheduleMeetingRequestDTO
    {
        public required string Title { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public required List<Guid> ParticipantIds { get; set; }
        public required string UserTimeZoneId { get; set; }
    }
}