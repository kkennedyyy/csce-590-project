using System.Globalization;
using ClassFinder.Api.Data;
using ClassFinder.Api.DTOs;
using ClassFinder.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ClassFinder.Api.Services;

public class RegistrationService(ClassFinderDbContext dbContext) : IRegistrationService
{
    private const int MaxCredits = 19;
    private const string DefaultTerm = "Fall 2026";

    public async Task<CloudAuthEnvelopeDto?> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        var role = request.Role.Trim().ToLowerInvariant();
        var email = request.Email.Trim().ToLowerInvariant();

        if (role == "student")
        {
            if (request.Password != "student123")
            {
                return null;
            }

            var student = await dbContext.Students
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Email.ToLower() == email, cancellationToken);

            if (student is null)
            {
                return null;
            }

            return new CloudAuthEnvelopeDto
            {
                User = new CloudAuthUserDto
                {
                    UserId = BuildStudentToken(student),
                    Role = "student",
                    Name = $"{student.FirstName} {student.LastName}",
                    Email = student.Email
                }
            };
        }

        if (role == "teacher")
        {
            if (request.Password != "teacher123")
            {
                return null;
            }

            Instructor? instructor;

            if (email == "dr.smith@email.com")
            {
                instructor = await dbContext.Instructors
                    .AsNoTracking()
                    .OrderBy(x => x.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            else
            {
                instructor = await dbContext.Instructors
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Email.ToLower() == email, cancellationToken);
            }

            if (instructor is null)
            {
                return null;
            }

            var teacherToken = await BuildTeacherTokenAsync(instructor.Id, cancellationToken);

            return new CloudAuthEnvelopeDto
            {
                User = new CloudAuthUserDto
                {
                    UserId = teacherToken,
                    Role = "teacher",
                    Name = $"{instructor.FirstName} {instructor.LastName}",
                    Email = instructor.Email
                }
            };
        }

        return null;
    }

    public async Task<CloudClassPageDto> GetClassesAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default
    )
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.CourseClasses
            .AsNoTracking()
            .Include(x => x.Instructor)
            .Include(x => x.Enrollments)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(
                x =>
                    EF.Functions.Like(x.CourseCode, $"%{term}%")
                    || EF.Functions.Like(x.ClassName, $"%{term}%")
                    || EF.Functions.Like(x.Location, $"%{term}%")
                    || EF.Functions.Like(x.Instructor!.FirstName, $"%{term}%")
                    || EF.Functions.Like(x.Instructor!.LastName, $"%{term}%")
            );
        }

        var total = await query.CountAsync(cancellationToken);

        var classes = await query
            .OrderBy(x => x.CourseCode)
            .ThenBy(x => x.Id)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return new CloudClassPageDto
        {
            Classes = classes.Select(MapCloudClass).ToList(),
            Page = safePage,
            PageSize = safePageSize,
            HasMore = safePage * safePageSize < total,
            Total = total
        };
    }

    public async Task<CloudClassDto?> GetClassByTokenAsync(
        string classToken,
        CancellationToken cancellationToken = default
    )
    {
        var courseClass = await ResolveClassAsync(classToken, null, cancellationToken);
        return courseClass is null ? null : MapCloudClass(courseClass);
    }

    public async Task<CloudStudentScheduleDto?> GetStudentScheduleStateAsync(
        string studentToken,
        CancellationToken cancellationToken = default
    )
    {
        var student = await ResolveStudentAsync(studentToken, cancellationToken);
        if (student is null)
        {
            return null;
        }

        var scheduleClasses = await dbContext.Enrollments
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id && x.Status == EnrollmentStatus.Enrolled)
            .Include(x => x.CourseClass)
            .ThenInclude(x => x!.Instructor)
            .OrderBy(x => x.CourseClass!.CourseCode)
            .Select(x => x.CourseClass!)
            .ToListAsync(cancellationToken);

        var mapped = scheduleClasses.Select(MapScheduledClass).ToList();

        return new CloudStudentScheduleDto
        {
            StudentId = studentToken,
            ScheduledClasses = mapped,
            CurrentCredits = mapped.Sum(x => x.Credits)
        };
    }

    public async Task<(CloudStudentScheduleDto? Schedule, RegistrationError? Error)> RegisterClassAsync(
        string studentToken,
        CloudScheduleMutationRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        var student = await ResolveStudentAsync(studentToken, cancellationToken);
        if (student is null)
        {
            return (null, new RegistrationError(StatusCodes.Status404NotFound, "Student was not found."));
        }

        var courseClass = await ResolveClassAsync(request.ClassId, request.SectionId, cancellationToken);
        if (courseClass is null)
        {
            return (null, new RegistrationError(StatusCodes.Status404NotFound, "Class unavailable."));
        }

        var existingEnrollment = await dbContext.Enrollments
            .AsNoTracking()
            .AnyAsync(
                x => x.StudentId == student.Id && x.CourseClassId == courseClass.Id,
                cancellationToken
            );

        if (existingEnrollment)
        {
            return (
                null,
                new RegistrationError(StatusCodes.Status409Conflict, "This class is already in your schedule.")
            );
        }

        var enrolledCount = await dbContext.Enrollments.CountAsync(
            x => x.CourseClassId == courseClass.Id && x.Status == EnrollmentStatus.Enrolled,
            cancellationToken
        );

        if (enrolledCount >= courseClass.Capacity)
        {
            return (
                null,
                new RegistrationError(
                    423,
                    $"{BuildExternalClassId(courseClass)} is full. Pick another section or try again later."
                )
            );
        }

        var existingEnrolledClasses = await dbContext.Enrollments
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id && x.Status == EnrollmentStatus.Enrolled)
            .Include(x => x.CourseClass)
            .Select(x => x.CourseClass!)
            .ToListAsync(cancellationToken);

        var currentCredits = existingEnrolledClasses.Sum(x => x.Credits);
        var attemptedCredits = currentCredits + courseClass.Credits;

        if (attemptedCredits > MaxCredits)
        {
            return (
                null,
                new RegistrationError(
                    StatusCodes.Status403Forbidden,
                    $"Adding {courseClass.Credits} credits raises total to {attemptedCredits} which exceeds the {MaxCredits} credit cap."
                )
            );
        }

        if (HasOverlap(existingEnrolledClasses, courseClass))
        {
            return (
                null,
                new RegistrationError(
                    StatusCodes.Status409Conflict,
                    "This class overlaps your existing schedule. Remove a conflict before adding it."
                )
            );
        }

        dbContext.Enrollments.Add(
            new Enrollment
            {
                StudentId = student.Id,
                CourseClassId = courseClass.Id,
                Status = EnrollmentStatus.Enrolled
            }
        );

        await dbContext.SaveChangesAsync(cancellationToken);

        var schedule = await GetStudentScheduleStateAsync(studentToken, cancellationToken);
        return (schedule, null);
    }

    public async Task<(CloudStudentScheduleDto? Schedule, RegistrationError? Error)> DeregisterClassAsync(
        string studentToken,
        string classOrSectionToken,
        CancellationToken cancellationToken = default
    )
    {
        var student = await ResolveStudentAsync(studentToken, cancellationToken);
        if (student is null)
        {
            return (null, new RegistrationError(StatusCodes.Status404NotFound, "Student was not found."));
        }

        var courseClass = await ResolveClassAsync(classOrSectionToken, null, cancellationToken);
        if (courseClass is null)
        {
            return (null, new RegistrationError(StatusCodes.Status404NotFound, "Class was not found."));
        }

        var enrollment = await dbContext.Enrollments.SingleOrDefaultAsync(
            x => x.StudentId == student.Id && x.CourseClassId == courseClass.Id,
            cancellationToken
        );

        if (enrollment is null)
        {
            var untouched = await GetStudentScheduleStateAsync(studentToken, cancellationToken);
            return (untouched, null);
        }

        dbContext.Enrollments.Remove(enrollment);
        await dbContext.SaveChangesAsync(cancellationToken);

        var schedule = await GetStudentScheduleStateAsync(studentToken, cancellationToken);
        return (schedule, null);
    }

    public async Task<(CloudStudentScheduleDto? Schedule, RegistrationError? Error)> FinalizeScheduleAsync(
        string studentToken,
        CloudFinalizeScheduleRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        var student = await ResolveStudentAsync(studentToken, cancellationToken);
        if (student is null)
        {
            return (null, new RegistrationError(StatusCodes.Status404NotFound, "Student was not found."));
        }

        var requestedItems = request.ScheduledClasses ?? [];
        var resolvedClasses = new List<CourseClass>(requestedItems.Count);

        foreach (var item in requestedItems)
        {
            var courseClass = await ResolveClassAsync(item.ClassId, item.SectionId, cancellationToken);
            if (courseClass is null)
            {
                var token = !string.IsNullOrWhiteSpace(item.ClassId)
                    ? item.ClassId
                    : item.SectionId?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
                return (null, new RegistrationError(StatusCodes.Status404NotFound, $"Class {token} was not found."));
            }

            resolvedClasses.Add(courseClass);
        }

        var duplicateClass = resolvedClasses
            .GroupBy(x => x.Id)
            .FirstOrDefault(group => group.Count() > 1)?
            .FirstOrDefault();
        if (duplicateClass is not null)
        {
            return (
                null,
                new RegistrationError(
                    StatusCodes.Status400BadRequest,
                    $"Duplicate class {BuildExternalClassId(duplicateClass)} detected in finalized schedule."
                )
            );
        }

        var desiredCredits = resolvedClasses.Sum(x => x.Credits);
        if (desiredCredits > MaxCredits)
        {
            return (
                null,
                new RegistrationError(
                    StatusCodes.Status403Forbidden,
                    $"Finalized schedule has {desiredCredits} credits and exceeds the {MaxCredits} credit cap."
                )
            );
        }

        for (var index = 0; index < resolvedClasses.Count; index += 1)
        {
            var candidate = resolvedClasses[index];
            if (HasOverlap(resolvedClasses.Take(index), candidate))
            {
                return (
                    null,
                    new RegistrationError(
                        StatusCodes.Status409Conflict,
                        $"Finalized schedule contains a time overlap involving {BuildExternalClassId(candidate)}."
                    )
                );
            }
        }

        var existingEnrolled = await dbContext.Enrollments
            .Where(
                x =>
                    x.StudentId == student.Id
                    && x.Status == EnrollmentStatus.Enrolled
            )
            .ToListAsync(cancellationToken);
        var existingClassIds = existingEnrolled.Select(x => x.CourseClassId).ToHashSet();
        var desiredClassIds = resolvedClasses.Select(x => x.Id).ToHashSet();

        var newlyRequestedClassIds = desiredClassIds.Where(id => !existingClassIds.Contains(id)).ToList();
        if (newlyRequestedClassIds.Count > 0)
        {
            var enrollmentCounts = await dbContext.Enrollments
                .AsNoTracking()
                .Where(
                    x =>
                        newlyRequestedClassIds.Contains(x.CourseClassId)
                        && x.Status == EnrollmentStatus.Enrolled
                )
                .GroupBy(x => x.CourseClassId)
                .Select(group => new { CourseClassId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(x => x.CourseClassId, x => x.Count, cancellationToken);

            foreach (var courseClass in resolvedClasses)
            {
                if (existingClassIds.Contains(courseClass.Id))
                {
                    continue;
                }

                var enrolledCount = enrollmentCounts.GetValueOrDefault(courseClass.Id, 0);
                if (enrolledCount >= courseClass.Capacity)
                {
                    return (
                        null,
                        new RegistrationError(
                            423,
                            $"{BuildExternalClassId(courseClass)} is full. Pick another section or try again later."
                        )
                    );
                }
            }
        }

        var enrollmentsToRemove = existingEnrolled
            .Where(x => !desiredClassIds.Contains(x.CourseClassId))
            .ToList();
        if (enrollmentsToRemove.Count > 0)
        {
            dbContext.Enrollments.RemoveRange(enrollmentsToRemove);
        }

        var enrollmentsToAdd = resolvedClasses
            .Where(x => !existingClassIds.Contains(x.Id))
            .Select(
                courseClass =>
                    new Enrollment
                    {
                        StudentId = student.Id,
                        CourseClassId = courseClass.Id,
                        Status = EnrollmentStatus.Enrolled
                    }
            )
            .ToList();

        if (enrollmentsToAdd.Count > 0)
        {
            dbContext.Enrollments.AddRange(enrollmentsToAdd);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var schedule = await GetStudentScheduleStateAsync(studentToken, cancellationToken);
        return (schedule, null);
    }

    public async Task<IReadOnlyList<CloudClassDto>?> GetTeacherClassesAsync(
        string teacherToken,
        CancellationToken cancellationToken = default
    )
    {
        var teacher = await ResolveInstructorAsync(teacherToken, cancellationToken);
        if (teacher is null)
        {
            return null;
        }

        var classes = await dbContext.CourseClasses
            .AsNoTracking()
            .Where(x => x.InstructorId == teacher.Id)
            .Include(x => x.Instructor)
            .Include(x => x.Enrollments)
            .OrderBy(x => x.CourseCode)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return classes.Select(MapCloudClass).ToList();
    }

    public async Task<CloudTeacherRosterDto?> GetTeacherRosterAsync(
        string teacherToken,
        string classToken,
        CancellationToken cancellationToken = default
    )
    {
        var teacher = await ResolveInstructorAsync(teacherToken, cancellationToken);
        if (teacher is null)
        {
            return null;
        }

        var classInfo = await ResolveClassAsync(classToken, null, cancellationToken);
        if (classInfo is null || classInfo.InstructorId != teacher.Id)
        {
            return null;
        }

        var enrollments = await dbContext.Enrollments
            .AsNoTracking()
            .Where(x => x.CourseClassId == classInfo.Id && x.Status == EnrollmentStatus.Enrolled)
            .Include(x => x.Student)
            .OrderBy(x => x.Student!.LastName)
            .ThenBy(x => x.Student!.FirstName)
            .ToListAsync(cancellationToken);

        return new CloudTeacherRosterDto
        {
            ClassInfo = MapCloudClass(classInfo),
            Students = enrollments
                .Select(
                    x =>
                        new CloudTeacherStudentDto
                        {
                            StudentId = BuildStudentToken(x.Student!),
                            Name = $"{x.Student!.FirstName} {x.Student.LastName}",
                            Email = x.Student.Email
                        }
                )
                .ToList()
        };
    }

    public async Task<(CloudClassDto? ClassInfo, RegistrationError? Error)> UpdateTeacherCapacityAsync(
        string teacherToken,
        string classToken,
        int capacity,
        CancellationToken cancellationToken = default
    )
    {
        var teacher = await ResolveInstructorAsync(teacherToken, cancellationToken);
        if (teacher is null)
        {
            return (null, new RegistrationError(StatusCodes.Status404NotFound, "Teacher was not found."));
        }

        var classInfo = await ResolveClassAsync(classToken, null, cancellationToken);
        if (classInfo is null || classInfo.InstructorId != teacher.Id)
        {
            return (null, new RegistrationError(StatusCodes.Status404NotFound, "Class was not found for this teacher."));
        }

        var enrolledCount = await dbContext.Enrollments.CountAsync(
            x => x.CourseClassId == classInfo.Id && x.Status == EnrollmentStatus.Enrolled,
            cancellationToken
        );

        if (capacity < enrolledCount)
        {
            return (
                null,
                new RegistrationError(
                    StatusCodes.Status400BadRequest,
                    $"Capacity cannot be lower than enrolled ({enrolledCount})."
                )
            );
        }

        classInfo.Capacity = capacity;
        await dbContext.SaveChangesAsync(cancellationToken);

        classInfo = await dbContext.CourseClasses
            .AsNoTracking()
            .Include(x => x.Instructor)
            .Include(x => x.Enrollments)
            .SingleAsync(x => x.Id == classInfo.Id, cancellationToken);

        return (MapCloudClass(classInfo), null);
    }

    public async Task<RegistrationError?> RemoveStudentFromClassAsync(
        string teacherToken,
        string classToken,
        string studentToken,
        CancellationToken cancellationToken = default
    )
    {
        var teacher = await ResolveInstructorAsync(teacherToken, cancellationToken);
        if (teacher is null)
        {
            return new RegistrationError(StatusCodes.Status404NotFound, "Teacher was not found.");
        }

        var classInfo = await ResolveClassAsync(classToken, null, cancellationToken);
        if (classInfo is null || classInfo.InstructorId != teacher.Id)
        {
            return new RegistrationError(StatusCodes.Status404NotFound, "Class was not found for this teacher.");
        }

        var student = await ResolveStudentAsync(studentToken, cancellationToken);
        if (student is null)
        {
            return new RegistrationError(StatusCodes.Status404NotFound, "Student was not found.");
        }

        var enrollment = await dbContext.Enrollments.SingleOrDefaultAsync(
            x => x.StudentId == student.Id && x.CourseClassId == classInfo.Id,
            cancellationToken
        );

        if (enrollment is null)
        {
            return null;
        }

        dbContext.Enrollments.Remove(enrollment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return null;
    }

    private async Task<Student?> ResolveStudentAsync(string studentToken, CancellationToken cancellationToken)
    {
        var token = studentToken.Trim();

        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId))
        {
            return await dbContext.Students.SingleOrDefaultAsync(x => x.Id == numericId, cancellationToken);
        }

        if (token.Contains('@'))
        {
            var email = token.ToLowerInvariant();
            return await dbContext.Students.SingleOrDefaultAsync(
                x => x.Email.ToLower() == email,
                cancellationToken
            );
        }

        if (token.Equals("student-123", StringComparison.OrdinalIgnoreCase))
        {
            var demoStudent = await dbContext.Students.SingleOrDefaultAsync(
                x => x.Email.ToLower() == "john.smith@email.com",
                cancellationToken
            );

            if (demoStudent is not null)
            {
                return demoStudent;
            }
        }

        if (token.StartsWith("student-", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = token["student-".Length..];
            if (int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var suffixId))
            {
                var byId = await dbContext.Students.SingleOrDefaultAsync(x => x.Id == suffixId, cancellationToken);
                if (byId is not null)
                {
                    return byId;
                }
            }
        }

        return null;
    }

    private async Task<Instructor?> ResolveInstructorAsync(string teacherToken, CancellationToken cancellationToken)
    {
        var token = teacherToken.Trim();

        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId))
        {
            return await dbContext.Instructors.SingleOrDefaultAsync(x => x.Id == numericId, cancellationToken);
        }

        if (token.Contains('@'))
        {
            var email = token.ToLowerInvariant();
            return await dbContext.Instructors.SingleOrDefaultAsync(
                x => x.Email.ToLower() == email,
                cancellationToken
            );
        }

        if (token.StartsWith("teacher-", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = token["teacher-".Length..];
            if (int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ordinal))
            {
                return await dbContext.Instructors
                    .OrderBy(x => x.Id)
                    .Skip(Math.Max(ordinal - 1, 0))
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        return null;
    }

    private async Task<CourseClass?> ResolveClassAsync(
        string? classToken,
        int? sectionId,
        CancellationToken cancellationToken
    )
    {
        var baseQuery = dbContext.CourseClasses
            .Include(x => x.Instructor)
            .Include(x => x.Enrollments)
            .AsQueryable();

        if (sectionId.HasValue)
        {
            return await baseQuery.SingleOrDefaultAsync(x => x.Id == sectionId.Value, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(classToken))
        {
            return null;
        }

        var token = classToken.Trim();

        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId))
        {
            return await baseQuery.SingleOrDefaultAsync(x => x.Id == numericId, cancellationToken);
        }

        if (token.Contains('-'))
        {
            var parts = token.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (
                parts.Length >= 2
                && int.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sectionFromToken)
            )
            {
                var bySection = await baseQuery.SingleOrDefaultAsync(
                    x => x.Id == sectionFromToken,
                    cancellationToken
                );
                if (bySection is not null)
                {
                    return bySection;
                }
            }

            token = parts[0];
        }

        return await baseQuery
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(x => x.CourseCode.ToLower() == token.ToLower(), cancellationToken);
    }

    private static CloudClassDto MapCloudClass(CourseClass source)
    {
        var instructorName = source.Instructor is null
            ? "TBD"
            : $"{source.Instructor.FirstName} {source.Instructor.LastName}";
        var enrolledCount = source.Enrollments.Count(x => x.Status == EnrollmentStatus.Enrolled);

        return new CloudClassDto
        {
            SectionId = source.Id,
            Id = BuildExternalClassId(source),
            Title = source.ClassName,
            Instructor = instructorName,
            Days = SplitDays(source.DaysOfWeek),
            StartTime = source.StartTime.ToString("HH:mm"),
            EndTime = source.EndTime.ToString("HH:mm"),
            Capacity = source.Capacity,
            EnrolledCount = enrolledCount,
            Credits = source.Credits,
            Room = source.Location,
            Location = source.Location,
            Term = DefaultTerm,
            ColorHint = ResolveColorHint(source.CourseCode)
        };
    }

    private static CloudScheduledClassDto MapScheduledClass(CourseClass source)
    {
        var instructorName = source.Instructor is null
            ? "TBD"
            : $"{source.Instructor.FirstName} {source.Instructor.LastName}";

        return new CloudScheduledClassDto
        {
            SectionId = source.Id,
            ClassId = BuildExternalClassId(source),
            Title = source.ClassName,
            Instructor = instructorName,
            Credits = source.Credits,
            Room = source.Location,
            Location = source.Location,
            Term = DefaultTerm,
            Days = SplitDays(source.DaysOfWeek),
            StartTime = source.StartTime.ToString("HH:mm"),
            EndTime = source.EndTime.ToString("HH:mm"),
            ColorHint = ResolveColorHint(source.CourseCode)
        };
    }

    private static string BuildExternalClassId(CourseClass source)
    {
        return $"{source.CourseCode}-{source.Id:00}";
    }

    private static IReadOnlyList<string> SplitDays(string csv)
    {
        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Length > 0 ? char.ToUpperInvariant(x[0]) + x[1..].ToLowerInvariant() : x)
            .ToList();
    }

    private static string ResolveColorHint(string courseCode)
    {
        if (courseCode.StartsWith("MATH", StringComparison.OrdinalIgnoreCase))
        {
            return "red";
        }

        if (courseCode.StartsWith("CSCE", StringComparison.OrdinalIgnoreCase))
        {
            return "purple";
        }

        return "neutral";
    }

    private static bool HasOverlap(IEnumerable<CourseClass> existingClasses, CourseClass candidate)
    {
        var candidateDays = SplitDays(candidate.DaysOfWeek).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return existingClasses.Any(
            existing =>
            {
                var existingDays = SplitDays(existing.DaysOfWeek);
                var sameDay = existingDays.Any(day => candidateDays.Contains(day));
                if (!sameDay)
                {
                    return false;
                }

                var existingStart = existing.StartTime.ToTimeSpan();
                var existingEnd = existing.EndTime.ToTimeSpan();
                var candidateStart = candidate.StartTime.ToTimeSpan();
                var candidateEnd = candidate.EndTime.ToTimeSpan();

                return candidateStart < existingEnd && existingStart < candidateEnd;
            }
        );
    }

    private async Task<string> BuildTeacherTokenAsync(int instructorId, CancellationToken cancellationToken)
    {
        var orderedIds = await dbContext.Instructors
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var index = orderedIds.FindIndex(id => id == instructorId);
        if (index < 0)
        {
            return instructorId.ToString(CultureInfo.InvariantCulture);
        }

        return $"teacher-{index + 1}";
    }

    private static string BuildStudentToken(Student student)
    {
        if (student.Email.Equals("john.smith@email.com", StringComparison.OrdinalIgnoreCase))
        {
            return "student-123";
        }

        return $"student-{student.Id}";
    }
}
