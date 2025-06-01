using BairesCalendar.Application.DTOs;
using BairesCalendar.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BairesCalendar.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MeetingsController(ISchedulingService schedulingService, ILogger<MeetingsController> logger) : ControllerBase
    {
        private readonly ISchedulingService _schedulingService = schedulingService;
        private readonly ILogger<MeetingsController> _logger = logger;

        /// <summary>
        /// Schedules a new meeting for a list of participants.
        /// </summary>
        /// <param name="request">Meeting scheduling details.</param>
        /// <returns>A response indicating success or failure, with suggested slots if conflicts occur.</returns>
        [HttpPost("schedule")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ScheduleMeetingResponseDTO))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ScheduleMeeting([FromBody] ScheduleMeetingRequestDTO request)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid ScheduleMeetingRequest received.");
                return BadRequest(ModelState);
            }

            var response = await _schedulingService.ScheduleMeetingAsync(request);

            if (response.Success)
            {
                return Ok(response);
            }
            else
            {
                if (response.SuggestedSlots != null && response.SuggestedSlots.Any())
                {
                    return Ok(response);
                }

                return BadRequest(response);
            }
        }
    }
}