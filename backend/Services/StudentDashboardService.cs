using ClassFinder.Api.Data;
using ClassFinder.Api.DTOs;
using ClassFinder.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassFinder.Api.Services;

public class StudentDashboardService(ClassFinderDbContext dbContext) : IStudentDashboardService
{
    public Task<bool> StudentExistsAsync(int studentId, CancellationToken cancellationToken = default)
    {
        return dbContext.Students.AnyAsync(x => x.Id == studentId, cancellationToken);
    }

    public async Task<IReadOnlyList<StudentClassDto>> GetStudentClassesAsync(
        int studentId,
        CancellationToken cancellationToken = default
    )
    {
        var enrollments = await dbContext.Enrollments
            .AsNoTracking()
            .Where(x => x.StudentId == studentId)
            .Include(x => x.CourseClass)
            .ThenInclude(x => x!.Instructor)
            .OrderBy(x => x.CourseClass!.CourseCode)
            .ToListAsync(cancellationToken);

        return enrollments
            .Select(
                enrollment =>
                {
                    var courseClass = enrollment.CourseClass!;
                    var instructor = courseClass.Instructor!;
                    return new StudentClassDto
                    {
                        ClassId = courseClass.Id,
                        ClassName = courseClass.ClassName,
                        CourseCode = courseClass.CourseCode,
                        InstructorName = $"{instructor.FirstName} {instructor.LastName}",
                        DaysTimes =
                            $"{courseClass.DaysOfWeek} {courseClass.StartTime:HH:mm}-{courseClass.EndTime:HH:mm}",
                        Location = courseClass.Location,
                        Credits = courseClass.Credits,
                        IsWaitlisted = enrollment.Status == EnrollmentStatus.Waitlisted,
                        WaitlistPosition = enrollment.WaitlistPosition
                    };
                }
            )
            .ToList();
    }

    public async Task<IReadOnlyList<ScheduleEventDto>> GetStudentScheduleAsync(
        int studentId,
        CancellationToken cancellationToken = default
    )
    {
        var enrollments = await dbContext.Enrollments
            .AsNoTracking()
            .Where(x => x.StudentId == studentId)
            .Include(x => x.CourseClass)
            .ThenInclude(x => x!.Instructor)
            .ToListAsync(cancellationToken);

        var scheduleEvents = new List<ScheduleEventDto>();

        foreach (var enrollment in enrollments)
        {
            var courseClass = enrollment.CourseClass!;
            var days = courseClass
                .DaysOfWeek.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            foreach (var day in days)
            {
                scheduleEvents.Add(
                    new ScheduleEventDto
                    {
                        ClassId = courseClass.Id,
                        ClassName = courseClass.ClassName,
                        CourseCode = courseClass.CourseCode,
                        DayOfWeek = day,
                        StartTime = courseClass.StartTime.ToString("HH:mm"),
                        EndTime = courseClass.EndTime.ToString("HH:mm"),
                        Location = courseClass.Location
                    }
                );
            }
        }

        return scheduleEvents
            .OrderBy(x => DayOrder(x.DayOfWeek))
            .ThenBy(x => x.StartTime)
            .ThenBy(x => x.CourseCode)
            .ToList();
    }

    private static int DayOrder(string dayOfWeek)
    {
        return dayOfWeek switch
        {
            "Mon" => 1,
            "Tue" => 2,
            "Wed" => 3,
            "Thu" => 4,
            "Fri" => 5,
            _ => 99
        };
    }
}
