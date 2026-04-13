using System.Text.Json;
using ClassFinder.Api.Data;
using ClassFinder.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassFinder.Api.Services;

public class FeedIngestionService(
    ClassFinderDbContext db,
    IConfiguration configuration,
    ILogger<FeedIngestionService> logger) : IFeedIngestionService
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task PollAndProcessAsync(CancellationToken cancellationToken = default)
    {
        var root = configuration["FeedStorage:RootPath"];
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            logger.LogWarning("Feed storage path '{RootPath}' does not exist.", root);
            return;
        }

        await ProcessUsersAsync(Path.Combine(root, "users"), cancellationToken);
        await ProcessClassesAsync(Path.Combine(root, "classes"), cancellationToken);
        await ProcessEnrollmentsAsync(Path.Combine(root, "enrollments"), cancellationToken);
    }

    private async Task ProcessUsersAsync(string folder, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folder)) return;

        foreach (var file in Directory.GetFiles(folder, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var items = JsonSerializer.Deserialize<List<UserFeedItem>>(json, _jsonOptions) ?? [];

                foreach (var item in items)
                {
                    var student = await db.Students
                        .SingleOrDefaultAsync(x => x.Email == item.Email, cancellationToken);

                    if (student is null)
                    {
                        db.Students.Add(new Student
                        {
                            Email = item.Email,
                            FirstName = item.FirstName,
                            LastName = item.LastName
                        });
                    }
                    else
                    {
                        student.FirstName = item.FirstName;
                        student.LastName = item.LastName;
                    }
                }

                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Processed users file: {File}", file);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed processing users file: {File}", file);
            }
        }
    }

    private async Task ProcessClassesAsync(string folder, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folder)) return;

        foreach (var file in Directory.GetFiles(folder, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var items = JsonSerializer.Deserialize<List<ClassFeedItem>>(json, _jsonOptions) ?? [];

                foreach (var item in items)
                {
                    var instructor = await db.Instructors
                        .SingleOrDefaultAsync(x => x.Email == item.InstructorEmail, cancellationToken);

                    if (instructor is null)
                    {
                        instructor = new Instructor
                        {
                            Email = item.InstructorEmail,
                            FirstName = item.InstructorFirstName,
                            LastName = item.InstructorLastName
                        };

                        db.Instructors.Add(instructor);
                        await db.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        instructor.FirstName = item.InstructorFirstName;
                        instructor.LastName = item.InstructorLastName;
                    }

                    var course = await db.CourseClasses
                        .SingleOrDefaultAsync(x => x.CourseCode == item.CourseCode, cancellationToken);

                    if (course is null)
                    {
                        db.CourseClasses.Add(new CourseClass
                        {
                            CourseCode = item.CourseCode,
                            ClassName = item.ClassName,
                            InstructorId = instructor.Id,
                            Location = item.Location,
                            Credits = item.Credits,
                            Capacity = item.Capacity,
                            DaysOfWeek = item.DaysOfWeek,
                            StartTime = TimeOnly.Parse(item.StartTime),
                            EndTime = TimeOnly.Parse(item.EndTime)
                        });
                    }
                    else
                    {
                        course.ClassName = item.ClassName;
                        course.InstructorId = instructor.Id;
                        course.Location = item.Location;
                        course.Credits = item.Credits;
                        course.Capacity = item.Capacity;
                        course.DaysOfWeek = item.DaysOfWeek;
                        course.StartTime = TimeOnly.Parse(item.StartTime);
                        course.EndTime = TimeOnly.Parse(item.EndTime);
                    }
                }

                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Processed classes file: {File}", file);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed processing classes file: {File}", file);
            }
        }
    }

    private async Task ProcessEnrollmentsAsync(string folder, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folder)) return;

        foreach (var file in Directory.GetFiles(folder, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var items = JsonSerializer.Deserialize<List<EnrollmentFeedItem>>(json, _jsonOptions) ?? [];

                foreach (var item in items)
                {
                    var student = await db.Students
                        .SingleOrDefaultAsync(x => x.Email == item.StudentEmail, cancellationToken);

                    if (student is null)
                    {
                        logger.LogWarning("Student not found for enrollment: {Email}", item.StudentEmail);
                        continue;
                    }

                    var course = await db.CourseClasses
                        .SingleOrDefaultAsync(x => x.CourseCode == item.CourseCode, cancellationToken);

                    if (course is null)
                    {
                        logger.LogWarning("Course not found for enrollment: {CourseCode}", item.CourseCode);
                        continue;
                    }

                    var enrollment = await db.Enrollments.SingleOrDefaultAsync(
                        x => x.StudentId == student.Id && x.CourseClassId == course.Id,
                        cancellationToken);

                    var parsedStatus = Enum.TryParse<EnrollmentStatus>(item.Status, true, out var status)
                        ? status
                        : EnrollmentStatus.Enrolled;

                    if (enrollment is null)
                    {
                        db.Enrollments.Add(new Enrollment
                        {
                            StudentId = student.Id,
                            CourseClassId = course.Id,
                            Status = parsedStatus,
                            WaitlistPosition = item.WaitlistPosition
                        });
                    }
                    else
                    {
                        enrollment.Status = parsedStatus;
                        enrollment.WaitlistPosition = item.WaitlistPosition;
                    }
                }

                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Processed enrollments file: {File}", file);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed processing enrollments file: {File}", file);
            }
        }
    }
}