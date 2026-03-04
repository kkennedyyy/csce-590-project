using ClassFinder.Api.Data;
using ClassFinder.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassFinder.Api.Services;

public class EnrollmentService(ClassFinderDbContext context) : IEnrollmentService
{
    public async Task<(bool Success, string Message)> EnrollAsync(int studentId, int classId, CancellationToken cancellationToken)
    {
        var student = await context.Students
            .Include(s => s.Enrollments)
            .ThenInclude(e => e.CourseClass)
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);

        if (student is null)
            return (false, "Student not found.");

        var courseClass = await context.CourseClasses
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);

        if (courseClass is null)
            return (false, "Class not found.");

        // Capacity check
        var enrolledCount = courseClass.Enrollments
            .Count(e => e.Status == EnrollmentStatus.Enrolled);

        if (enrolledCount >= courseClass.Capacity)
            return (false, "Class is full.");

        // Schedule conflict check
        var studentClasses = student.Enrollments
            .Where(e => e.Status == EnrollmentStatus.Enrolled)
            .Select(e => e.CourseClass);

        foreach (var existing in studentClasses)
        {
            if (existing!.DaysOfWeek == courseClass.DaysOfWeek &&
                existing.StartTime < courseClass.EndTime &&
                courseClass.StartTime < existing.EndTime)
            {
                return (false, "Schedule conflict detected.");
            }
        }

        var enrollment = new Enrollment
        {
            StudentId = studentId,
            CourseClassId = classId,
            Status = EnrollmentStatus.Enrolled
        };

        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync(cancellationToken);

        return (true, "Enrollment successful.");
    }

    public async Task<(bool Success, string Message)> DropAsync(int studentId, int classId, CancellationToken cancellationToken)
    {
        var enrollment = await context.Enrollments
            .FirstOrDefaultAsync(e => e.StudentId == studentId && e.CourseClassId == classId, cancellationToken);

        if (enrollment is null)
            return (false, "Enrollment not found.");

        context.Enrollments.Remove(enrollment);
        await context.SaveChangesAsync(cancellationToken);

        return (true, "Class dropped successfully.");
    }
}