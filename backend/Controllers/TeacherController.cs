using ClassFinder.Api.DTOs;
using ClassFinder.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClassFinder.Api.Controllers;

[ApiController]
[Route("api/teachers")]
public class TeacherController(IRegistrationService registrationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeacherCatalog(
        [FromQuery] string? search = null,
        [FromQuery] string? department = null,
        [FromQuery] string? studentId = null,
        CancellationToken cancellationToken = default
    )
    {
        var teachers = await registrationService.GetTeacherCatalogAsync(
            search,
            department,
            studentId,
            cancellationToken
        );
        return Ok(teachers);
    }

    [HttpGet("{teacherId}/classes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTeacherClasses(string teacherId, CancellationToken cancellationToken)
    {
        var classes = await registrationService.GetTeacherClassesAsync(teacherId, cancellationToken);
        if (classes is null)
        {
            return NotFound(new { message = "Teacher profile not found." });
        }

        return Ok(new { classes });
    }

    [HttpGet("{teacherId}/classes/{classToken}/roster")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoster(
        string teacherId,
        string classToken,
        CancellationToken cancellationToken
    )
    {
        var (roster, error) = await registrationService.GetTeacherRosterAsync(
            teacherId,
            classToken,
            cancellationToken
        );
        if (error is not null)
        {
            return StatusCode(error.StatusCode, new { message = error.Message });
        }

        return Ok(roster);
    }

    [HttpPut("{teacherId}/classes/{classToken}/capacity")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCapacity(
        string teacherId,
        string classToken,
        [FromBody] CapacityRequest request,
        CancellationToken cancellationToken
    )
    {
        var (classInfo, error) = await registrationService.UpdateTeacherCapacityAsync(
            teacherId,
            classToken,
            request.Capacity,
            cancellationToken
        );

        if (error is not null)
        {
            return StatusCode(error.StatusCode, new { message = error.Message });
        }

        return Ok(classInfo);
    }

    [HttpPut("{teacherId}/classes/{classToken}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateClass(
        string teacherId,
        string classToken,
        [FromBody] TeacherClassUpdateRequestDto request,
        CancellationToken cancellationToken
    )
    {
        var (classInfo, error) = await registrationService.UpdateTeacherClassAsync(
            teacherId,
            classToken,
            request,
            cancellationToken
        );

        if (error is not null)
        {
            return StatusCode(error.StatusCode, new { message = error.Message });
        }

        return Ok(classInfo);
    }

    [HttpDelete("{teacherId}/classes/{classToken}/students/{studentToken}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveStudent(
        string teacherId,
        string classToken,
        string studentToken,
        CancellationToken cancellationToken
    )
    {
        var error = await registrationService.RemoveStudentFromClassAsync(
            teacherId,
            classToken,
            studentToken,
            cancellationToken
        );

        if (error is not null)
        {
            return StatusCode(error.StatusCode, new { message = error.Message });
        }

        return NoContent();
    }

    public class CapacityRequest
    {
        public int Capacity { get; set; }
    }
}
