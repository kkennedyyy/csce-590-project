using ClassFinder.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClassFinder.Api.Controllers;

[ApiController]
[Route("api/enrollments")]
public class EnrollmentController(IEnrollmentService enrollmentService) : ControllerBase
{
    [HttpPost("enroll")]
    public async Task<IActionResult> Enroll(int studentId, int classId, CancellationToken cancellationToken)
    {
        var result = await enrollmentService.EnrollAsync(studentId, classId, cancellationToken);

        if (!result.Success)
            return BadRequest(new { message = result.Message });

        return Ok(new { message = result.Message });
    }

    [HttpDelete("drop")]
    public async Task<IActionResult> Drop(int studentId, int classId, CancellationToken cancellationToken)
    {
        var result = await enrollmentService.DropAsync(studentId, classId, cancellationToken);

        if (!result.Success)
            return BadRequest(new { message = result.Message });

        return Ok(new { message = result.Message });
    }
}