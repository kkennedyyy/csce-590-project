using ClassFinder.Api.DTOs;
using ClassFinder.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClassFinder.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IRegistrationService registrationService) : ControllerBase
{
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var auth = await registrationService.LoginAsync(request, cancellationToken);
        if (auth is null)
        {
            return Unauthorized(new { message = "Invalid credentials." });
        }

        return Ok(auth);
    }

    [HttpPost("signup/student")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SignupStudent(
        [FromBody] StudentSignupRequestDto request,
        CancellationToken cancellationToken
    )
    {
        var (auth, error) = await registrationService.RegisterStudentAsync(request, cancellationToken);
        if (error is not null)
        {
            return StatusCode(error.StatusCode, new { message = error.Message });
        }

        return StatusCode(StatusCodes.Status201Created, auth);
    }
}
