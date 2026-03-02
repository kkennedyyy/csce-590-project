using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

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
    public async Task<IActionResult> Run(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "classes/{sectionId:int}")]
    HttpRequest req,
    int sectionId)
    {
        _logger.LogInformation($"Fetching details for section {sectionId}");

        string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

        if (string.IsNullOrEmpty(connectionString))
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);

        using SqlConnection conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        string query = @"
            SELECT 
                cs.SectionId,
                c.CourseName,
                c.CourseCode,
                c.Credits,
                cs.Capacity,
                i.Firstname + ' ' + i.Lastname AS InstructorName,
                sch.DayOfWeek,
                sch.StartTime,
                sch.EndTime,
                sch.Location
            FROM ClassSections cs
            JOIN Courses c ON cs.CourseId = c.CourseId
            JOIN Instructors i ON cs.InstructorId = i.InstructorId
            LEFT JOIN ClassSchedule sch ON cs.SectionId = sch.SectionId
            WHERE cs.SectionId = @SectionId";

        SqlCommand cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@SectionId", sectionId);

        SqlDataReader reader = await cmd.ExecuteReaderAsync();

        if (!reader.HasRows)
            return new NotFoundObjectResult(new { message = "Class not found." });

        var schedules = new List<object>();
        string courseName = "";
        string courseCode = "";
        string instructor = "";
        int credits = 0;
        int capacity = 0;

        while (await reader.ReadAsync())
        {
            courseName = reader["CourseName"].ToString();
            courseCode = reader["CourseCode"].ToString();
            instructor = reader["InstructorName"].ToString();
            credits = (int)reader["Credits"];
            capacity = (int)reader["Capacity"];

            if (reader["DayOfWeek"] != DBNull.Value)
            {
                schedules.Add(new
                {
                    day = reader["DayOfWeek"],
                    startTime = reader["StartTime"],
                    endTime = reader["EndTime"],
                    location = reader["Location"]
                });
            }
        }

        return new OkObjectResult(new
        {
            sectionId,
            courseName,
            courseCode,
            instructor,
            credits,
            capacity,
            schedule = schedules
        });
    }
}