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
}
