using ClassFinder.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClassFinder.Api.Controllers;

[ApiController]
[Route("api/classes")]
public class ClassController(IClassService classService) : ControllerBase
{
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
