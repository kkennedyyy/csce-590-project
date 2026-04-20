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
            .Where(
                x =>
                    x.StudentId == studentId
                    && (x.Status == EnrollmentStatus.Enrolled || x.Status == EnrollmentStatus.Waitlisted)
            )
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
                    var parsedDays = ParseDays(courseClass.DaysOfWeek);
                    return new StudentClassDto
                    {
                        ClassId = courseClass.Id,
                        ClassName = courseClass.ClassName,
                        CourseCode = courseClass.CourseCode,
                        InstructorName = $"{instructor.FirstName} {instructor.LastName}",
                        DaysTimes =
                            $"{string.Join("/", parsedDays)} {courseClass.StartTime:HH:mm}-{courseClass.EndTime:HH:mm}",
                        Days = parsedDays,
                        StartTime = courseClass.StartTime.ToString("HH:mm"),
                        EndTime = courseClass.EndTime.ToString("HH:mm"),
                        Location = courseClass.Location,
                        Credits = courseClass.Credits,
                        Role = "enrolled",
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
            .Where(
                x =>
                    x.StudentId == studentId
                    && (x.Status == EnrollmentStatus.Enrolled || x.Status == EnrollmentStatus.Waitlisted)
            )
            .Include(x => x.CourseClass)
            .ThenInclude(x => x!.Instructor)
            .ToListAsync(cancellationToken);

        var scheduleEvents = new List<ScheduleEventDto>();

        foreach (var enrollment in enrollments)
        {
            var courseClass = enrollment.CourseClass!;
            var days = ParseDays(courseClass.DaysOfWeek);

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

    private static readonly Dictionary<string, string> DayAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["M"] = "Mon", ["MON"] = "Mon", ["MONDAY"] = "Mon",
        ["T"] = "Tue", ["TUE"] = "Tue", ["TUES"] = "Tue", ["TUESDAY"] = "Tue",
        ["W"] = "Wed", ["WED"] = "Wed", ["WEDNESDAY"] = "Wed",
        ["R"] = "Thu", ["TH"] = "Thu", ["THU"] = "Thu", ["THUR"] = "Thu", ["THURSDAY"] = "Thu",
        ["F"] = "Fri", ["FRI"] = "Fri", ["FRIDAY"] = "Fri",
    };

    private static IReadOnlyList<string> ParseDays(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var delimited = raw.Split([',', '/', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (delimited.Length > 1)
        {
            return delimited
                .Select(token => DayAliases.TryGetValue(token, out var mapped) ? mapped : token)
                .Where(d => d is "Mon" or "Tue" or "Wed" or "Thu" or "Fri")
                .Distinct()
                .ToList();
        }

        // Compact codes: MWF, TR, MW, etc.
        var compact = raw.Trim();
        var result = new List<string>();
        int i = 0;
        while (i < compact.Length)
        {
            if (i + 1 < compact.Length && compact[i] == 'T' && compact[i + 1] == 'H')
            {
                result.Add("Thu");
                i += 2;
            }
            else if (compact[i] == 'M') { result.Add("Mon"); i++; }
            else if (compact[i] == 'T') { result.Add("Tue"); i++; }
            else if (compact[i] == 'W') { result.Add("Wed"); i++; }
            else if (compact[i] == 'R') { result.Add("Thu"); i++; }
            else if (compact[i] == 'F') { result.Add("Fri"); i++; }
            else i++;
        }

        return result.Count > 0 ? result : delimited
            .Select(token => DayAliases.TryGetValue(token, out var mapped) ? mapped : token)
            .Where(d => d is "Mon" or "Tue" or "Wed" or "Thu" or "Fri")
            .ToList();
    }
}
