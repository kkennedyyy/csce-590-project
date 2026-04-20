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

        if (courseClass.Enrollments.Any(e => e.StudentId == studentId))
            return (false, "Student already enrolled or waitlisted.");

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

        // Capacity check
        var enrolledCount = courseClass.Enrollments
            .Count(e => e.Status == EnrollmentStatus.Enrolled);

        
        if (enrolledCount >= courseClass.Capacity)
        {
            var maxPosition = courseClass.Enrollments
                .Where(e => e.Status == EnrollmentStatus.Waitlisted)
                .Select(e => e.WaitlistPosition ?? 0)
                .DefaultIfEmpty(0)
                .Max();

            var waitlistEnrollment = new Enrollment
            {
                StudentId = studentId,
                CourseClassId = classId,
                Status = EnrollmentStatus.Waitlisted,
                WaitlistPosition = maxPosition + 1
            };

            context.Enrollments.Add(waitlistEnrollment);
            await context.SaveChangesAsync(cancellationToken);

            return (true, $"Class is full. Added to waitlist at position {maxPosition + 1}.");
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

        var wasEnrolled = enrollment.Status == EnrollmentStatus.Enrolled;

        context.Enrollments.Remove(enrollment);
        await context.SaveChangesAsync(cancellationToken);

        
        if (wasEnrolled)
        {
            var nextStudent = await context.Enrollments
                .Where(e => e.CourseClassId == classId && e.Status == EnrollmentStatus.Waitlisted)
                .OrderBy(e => e.WaitlistPosition)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextStudent != null)
            {
                nextStudent.Status = EnrollmentStatus.Enrolled;
                nextStudent.WaitlistPosition = null;

                var others = await context.Enrollments
                    .Where(e => e.CourseClassId == classId &&
                                e.Status == EnrollmentStatus.Waitlisted &&
                                e.Id != nextStudent.Id)
                    .ToListAsync(cancellationToken);

                foreach (var e in others)
                {
                    if (e.WaitlistPosition != null)
                        e.WaitlistPosition--;
                }

                await context.SaveChangesAsync(cancellationToken);
            }
        }

        return (true, "Class dropped successfully.");
    }

    public async Task<(bool Success, string Message)> AcceptScheduleAsync(
        int studentId,
        List<int> classIds,
        CancellationToken cancellationToken
    )
    {
        if (classIds.Count == 0)
        {
            return (false, "No classes supplied.");
        }

        foreach (var classId in classIds.Distinct())
        {
            var result = await EnrollAsync(studentId, classId, cancellationToken);
            if (!result.Success)
            {
                return result;
            }
        }

        return (true, "Schedule accepted and enrolled.");
    }
}
