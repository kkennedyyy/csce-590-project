using ClassFinder.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClassFinder.Api.Controllers;

[ApiController]
[Route("api/students")]
public class StudentController(IStudentDashboardService studentDashboardService) : ControllerBase
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
}
