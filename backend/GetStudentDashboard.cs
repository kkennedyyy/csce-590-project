using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace csce_590_project;
// <summary>
// HTTP Trigger function that returns dashboard data for a specific student.
//
// Function Should:
// 1. Retrieve all classes the student is currently enrolled in
// 2. Each class show class name, instructor, course code, instructor name, Days/time, location and credits
// 3. Include waitlist status if applicable
// 4. Return results in a structured JSON format for frontend consumption
//</summary>

public class GetStudentDashboard
{
    private readonly ILogger<GetStudentDashboard> _logger;

    public GetStudentDashboard(ILogger<GetStudentDashboard> logger)
    {
        _logger = logger;
    }

    [Function("GetStudentDashboard")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        return new OkObjectResult("Welcome to Azure Functions!");
    }
}
