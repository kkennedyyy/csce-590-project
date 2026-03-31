using ClassFinder.Api.DTOs;
using ClassFinder.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClassFinder.Api.Controllers;

[ApiController]
[Route("api/students")]
public class StudentController(
    IStudentDashboardService studentDashboardService,
    IRegistrationService registrationService
) : ControllerBase
{
    [HttpGet("{id:int}/classes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentClasses(int id, CancellationToken cancellationToken)
    {
        var studentExists = await studentDashboardService.StudentExistsAsync(id, cancellationToken);
        if (!studentExists)
        {
            return NotFound(new { message = $"Student with id {id} was not found." });
        }

        var classes = await studentDashboardService.GetStudentClassesAsync(id, cancellationToken);
        return Ok(classes);
    }

    [HttpGet("{id:int}/schedule")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentSchedule(int id, CancellationToken cancellationToken)
    {
        var studentExists = await studentDashboardService.StudentExistsAsync(id, cancellationToken);
        if (!studentExists)
        {
            return NotFound(new { message = $"Student with id {id} was not found." });
        }

        var events = await studentDashboardService.GetStudentScheduleAsync(id, cancellationToken);
        return Ok(events);
    }

    [HttpGet("{id}/schedule/state")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentScheduleState(string id, CancellationToken cancellationToken)
    {
        var schedule = await registrationService.GetStudentScheduleStateAsync(id, cancellationToken);
        if (schedule is null)
        {
            return NotFound(new { message = $"Student {id} was not found." });
        }

        return Ok(schedule);
    }

    [HttpPost("{id}/schedule")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(423)]
    public async Task<IActionResult> RegisterClass(
        string id,
        [FromBody] CloudScheduleMutationRequestDto request,
        CancellationToken cancellationToken
    )
    {
        var (schedule, error) = await registrationService.RegisterClassAsync(id, request, cancellationToken);
        if (error is not null)
        {
            return StatusCode(error.StatusCode, new { message = error.Message });
        }

        return Ok(schedule);
    }

    [HttpDelete("{id}/schedule/{classIdOrSection}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeregisterClass(
        string id,
        string classIdOrSection,
        CancellationToken cancellationToken
    )
    {
        var (schedule, error) = await registrationService.DeregisterClassAsync(
            id,
            classIdOrSection,
            cancellationToken
        );
        if (error is not null)
        {
            return StatusCode(error.StatusCode, new { message = error.Message });
        }

        return Ok(schedule);
    }

    [HttpPost("{id}/schedule/finalize")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(423)]
    public async Task<IActionResult> FinalizeSchedule(
        string id,
        [FromBody] CloudFinalizeScheduleRequestDto request,
        CancellationToken cancellationToken
    )
    {
        var (schedule, error) = await registrationService.FinalizeScheduleAsync(id, request, cancellationToken);
        if (error is not null)
        {
            return StatusCode(error.StatusCode, new { message = error.Message });
        }

        return Ok(schedule);
    }
}
