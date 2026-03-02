using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data.SqlTypes;

namespace csce_590_project{
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
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "students/{studentId:int}")] 
        HttpRequest req,
        int studentId)
    {
        _logger.LogInformation("Getting dashboard for student " + studentId);

        string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
        

        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogError("SQL connection string is not set.");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }

        using SqlConnection conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        
            string query = @"
                SELECT 
                    cs.SectionId,
                    c.CourseName,
                    c.CourseCode,
                    c.Credits,
                    i.Firstname + ' ' + i.Lastname AS InstructorName,
                    sch.DayOfWeek,
                    sch.StartTime,
                    sch.EndTime,
                    sch.Location

                FROM Enrollments e
                JOIN ClassSections cs ON e.SectionId = cs.SectionId
                JOIN Courses c ON cs.CourseId = c.CourseId
                JOIN Instructors i ON cs.InstructorId = i.InstructorId
                LEFT JOIN ClassSchedule sch ON cs.SectionId = sch.SectionId

                WHERE e.StudentId = @StudentId";
                

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@StudentId", studentId);

                SqlDataReader reader = await cmd.ExecuteReaderAsync();

                var classes = new List<object>();

                while (await reader.ReadAsync())
                {
                    classes.Add(new
                    {
                        id = reader["SectionId"],
                        className = reader["CourseName"],
                        courseCode = reader["CourseCode"],
                        instructor = reader["InstructorName"],
                        schedule = reader["DayOfWeek"] == DBNull.Value
                            ? "TBA"
                            : $"{reader["DayOfWeek"]} {reader["StartTime"]}-{reader["EndTime"]}",
                        location = reader["Location"] == DBNull.Value
                            ? "TBA"
                            : reader["Location"],
                        credits = reader["Credits"]
                    });
                }
                return new OkObjectResult(new
                {
                    studentId = studentId,
                    enrolledClasses = classes
                });
    }
}
}
