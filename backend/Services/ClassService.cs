using ClassFinder.Api.Data;
using ClassFinder.Api.DTOs;
using ClassFinder.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassFinder.Api.Services;

public class ClassService(ClassFinderDbContext dbContext) : IClassService
{
    public async Task<ClassDetailDto?> GetClassDetailsAsync(
        int classId,
        CancellationToken cancellationToken = default
    )
    {
        var courseClass = await dbContext.CourseClasses
            .AsNoTracking()
            .Where(x => x.Id == classId)
            .Include(x => x.Instructor)
            .Include(x => x.Enrollments)
            .ThenInclude(x => x.Student)
            .SingleOrDefaultAsync(cancellationToken);

        if (courseClass is null)
        {
            return null;
        }

        var enrolledCount = courseClass.Enrollments.Count(x => x.Status == EnrollmentStatus.Enrolled);
        var waitlist = courseClass
            .Enrollments.Where(x => x.Status == EnrollmentStatus.Waitlisted)
            .OrderBy(x => x.WaitlistPosition)
            .Select(
                x =>
                    new WaitlistEntryDto
                    {
                        StudentId = x.StudentId,
                        StudentName =
                            x.Student is null ? $"Student {x.StudentId}" : $"{x.Student.FirstName} {x.Student.LastName}",
                        Position = x.WaitlistPosition ?? 0
                    }
            )
            .ToList();

        return new ClassDetailDto
        {
            ClassId = courseClass.Id,
            ClassName = courseClass.ClassName,
            CourseCode = courseClass.CourseCode,
            Professor = $"{courseClass.Instructor!.FirstName} {courseClass.Instructor.LastName}",
            Capacity = courseClass.Capacity,
            EnrolledCount = enrolledCount,
            IsAtCapacity = enrolledCount >= courseClass.Capacity,
            WaitlistCount = waitlist.Count,
            Location = courseClass.Location,
            DaysOfWeek = courseClass.DaysOfWeek,
            StartTime = courseClass.StartTime.ToString("HH:mm"),
            EndTime = courseClass.EndTime.ToString("HH:mm"),
            Credits = courseClass.Credits,
            WaitlistPositions = waitlist
        };
    }
}
