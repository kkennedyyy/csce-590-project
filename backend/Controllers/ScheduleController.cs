using ClassFinder.Api.DTOs;
using ClassFinder.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClassFinder.Api.Controllers;

[ApiController]
[Route("api/schedules")]
public class ScheduleController(IScheduleGenerationService scheduleService, IEnrollmentService enrollmentService) : ControllerBase
{
    [HttpPost("request")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestSchedule(ScheduleRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var schedules = await scheduleService.GenerateSchedulesAsync(request, cancellationToken);
            return Ok(schedules);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("accept")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AcceptSchedule(ScheduleAcceptanceDto acceptance, CancellationToken cancellationToken)
    {
        var result = await enrollmentService.AcceptScheduleAsync(acceptance.StudentId, acceptance.ClassIds, cancellationToken);
        if (!result.Success)
            return BadRequest(new { message = result.Message });
            
        return Ok(new { message = "Schedule accepted and enrolled." });
    }
}