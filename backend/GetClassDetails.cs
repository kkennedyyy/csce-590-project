using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace csce_590_project;
// <summary>
// HTTP Trigger function that returns detailed info about a class.
//
// Function Should:
// 1. Retrieve all details about a specific class
// 2. Include instructor, capacity, location, credits, and days/times
// 3. Return 404 if class does not exist
//</summary>

public class GetClassDetails
{
    private readonly ILogger<GetClassDetails> _logger;

    public GetClassDetails(ILogger<GetClassDetails> logger)
    {
        _logger = logger;
    }

    [Function("GetClassDetails")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        return new OkObjectResult("Welcome to Azure Functions!");
    }
}
