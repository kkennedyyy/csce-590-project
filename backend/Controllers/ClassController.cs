using ClassFinder.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClassFinder.Api.Controllers;

[ApiController]
[Route("api/classes")]
public class ClassController(IClassService classService, IRegistrationService registrationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClasses(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default
    )
    {
        var response = await registrationService.GetClassesAsync(page, pageSize, search, cancellationToken);
        return Ok(response);
    }

    [HttpGet("by/{idOrSection}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClassByToken(string idOrSection, CancellationToken cancellationToken)
    {
        var classInfo = await registrationService.GetClassByTokenAsync(idOrSection, cancellationToken);
        if (classInfo is null)
        {
            return NotFound(new { message = $"Class {idOrSection} was not found." });
        }

        return Ok(classInfo);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClassById(int id, CancellationToken cancellationToken)
    {
        var classDetails = await classService.GetClassDetailsAsync(id, cancellationToken);
        if (classDetails is null)
        {
            return NotFound(new { message = $"Class with id {id} was not found." });
        }

        return Ok(classDetails);
    }
}
