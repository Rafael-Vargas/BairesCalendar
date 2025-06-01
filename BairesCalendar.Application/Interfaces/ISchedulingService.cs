using BairesCalendar.Application.DTOs;

namespace BairesCalendar.Application.Interfaces
{
    public interface ISchedulingService
    {
        Task<ScheduleMeetingResponseDTO> ScheduleMeetingAsync(ScheduleMeetingRequestDTO request);
    }
}