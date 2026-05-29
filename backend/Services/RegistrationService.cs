using System.Globalization;
using System.Net.Mail;
using ClassFinder.Api.Data;
using ClassFinder.Api.DTOs;
using ClassFinder.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ClassFinder.Api.Services;

public class RegistrationService(
    ClassFinderDbContext dbContext,
    IEnrollmentNotificationService enrollmentNotificationService,
    ILogger<RegistrationService> logger
) : IRegistrationService
{
    private const int MaxCredits = 19;
    private const string DefaultTerm = "Fall 2026";
    private const string ApplicationSource = "Application";
    private sealed record StudentEnrollmentInfo(EnrollmentStatus Status, int? WaitlistPosition);

    public async Task<CloudAuthEnvelopeDto?> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        var role = request.Role.Trim().ToLowerInvariant();
        var email = request.Email.Trim().ToLowerInvariant();

        if (role == "student")
        {
            var student = await dbContext.Students
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Email.ToLower() == email, cancellationToken);

            if (student is null || !MatchesPassword(student.Password, request.Password, "student123"))
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

            if (instructor is null || !MatchesPassword(instructor.Password, request.Password, "teacher123"))
            {
                return null;
            }

            return new CloudAuthEnvelopeDto
            {
                User = new CloudAuthUserDto
                {
                    UserId = await BuildTeacherTokenAsync(instructor.Id, cancellationToken),
                    Role = "teacher",
                    Name = $"{instructor.FirstName} {instructor.LastName}",
                    Email = instructor.Email
                }
            };
        }

        return null;
    }

    public async Task<(CloudAuthEnvelopeDto? Auth, RegistrationError? Error)> RegisterStudentAsync(
        StudentSignupRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var password = request.Password.Trim();
        var major = string.IsNullOrWhiteSpace(request.Major) ? "Undeclared" : request.Major.Trim();
        var classification = string.IsNullOrWhiteSpace(request.Classification)
            ? "Undergraduate"
            : request.Classification.Trim();

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            return (
                null,
                new RegistrationError(
                    StatusCodes.Status400BadRequest,
                    "First name and last name are required."
                )
            );
        }

        if (!IsValidEmail(email))
        {
            return (null, new RegistrationError(StatusCodes.Status400BadRequest, "Enter a valid email address."));
        }

        if (password.Length < 8)
        {
            return (
                null,
                new RegistrationError(
                    StatusCodes.Status400BadRequest,
                    "Password must be at least 8 characters long."
                )
            );
        }

        var emailInUse = await dbContext.Students.AnyAsync(
                x => x.Email.ToLower() == email,
                cancellationToken
            )
            || await dbContext.Instructors.AnyAsync(x => x.Email.ToLower() == email, cancellationToken);

        if (emailInUse)
        {
            return (
                null,
                new RegistrationError(
                    StatusCodes.Status409Conflict,
                    "An account already exists for that email address."
                )
            );
        }

        var student = new Student
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Password = PasswordSecurity.HashPassword(password),
            Major = major,
            Classification = classification
        };

        dbContext.Students.Add(student);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (
            new CloudAuthEnvelopeDto
            {
                User = new CloudAuthUserDto
                {
                    UserId = BuildStudentToken(student),
                    Role = "student",
                    Name = $"{student.FirstName} {student.LastName}",
                    Email = student.Email
                }
            },
            null
        );
    }

    public async Task<CloudClassPageDto> GetClassesAsync(
        int page,
        int pageSize,
        string? search,
        string? department,
        string? studentToken,
        CancellationToken cancellationToken = default
    )
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.CourseClasses
            .AsNoTracking()
            .Include(x => x.Instructor)
            .Include(x => x.Enrollments)
            .Include(x => x.Prerequisites)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(
                x =>
                    EF.Functions.Like(x.CourseCode, $"%{term}%")
                    || EF.Functions.Like(x.ClassName, $"%{term}%")
                    || EF.Functions.Like(x.Department, $"%{term}%")
                    || EF.Functions.Like(x.DepartmentCode, $"%{term}%")
                    || EF.Functions.Like(x.Location, $"%{term}%")
                    || EF.Functions.Like(x.Instructor!.FirstName, $"%{term}%")
                    || EF.Functions.Like(x.Instructor!.LastName, $"%{term}%")
            );
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            var filter = department.Trim();
            query = query.Where(
                x =>
                    x.DepartmentCode == filter
                    || x.Department == filter
                    || EF.Functions.Like(x.Department, $"%{filter}%")
            );
        }

        var total = await query.CountAsync(cancellationToken);
        var classes = await query
            .OrderBy(x => x.DepartmentCode)
            .ThenBy(x => x.CourseNumber)
            .ThenBy(x => x.SessionCode)
            .ThenBy(x => x.Id)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        var studentStatuses = await BuildStudentEnrollmentLookupAsync(
            studentToken,
            classes.Select(x => x.Id),
            cancellationToken
        );
        var departments = await dbContext.CourseClasses
            .AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.Department))
            .Select(x => x.Department)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        return new CloudClassPageDto
        {
            Classes = classes.Select(item => MapCloudClass(item, studentStatuses.GetValueOrDefault(item.Id))).ToList(),
            Departments = departments,
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

        var activeEnrollments = await dbContext.Enrollments
            .AsNoTracking()
            .Where(
                x =>
                    x.StudentId == student.Id
                    && (x.Status == EnrollmentStatus.Enrolled || x.Status == EnrollmentStatus.Waitlisted)
            )
            .Include(x => x.CourseClass)
            .ThenInclude(x => x!.Instructor)
            .OrderBy(x => x.CourseClass!.DepartmentCode)
            .ThenBy(x => x.CourseClass!.CourseNumber)
            .ThenBy(x => x.CourseClass!.SessionCode)
            .ToListAsync(cancellationToken);
        var courseClassIds = activeEnrollments.Select(x => x.CourseClassId).Distinct().ToList();
        var enrolledCounts = courseClassIds.Count == 0
            ? new Dictionary<int, int>()
            : await dbContext.Enrollments
                .AsNoTracking()
                .Where(
                    x =>
                        courseClassIds.Contains(x.CourseClassId)
                        && x.Status == EnrollmentStatus.Enrolled
                )
                .GroupBy(x => x.CourseClassId)
                .Select(group => new { CourseClassId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(x => x.CourseClassId, x => x.Count, cancellationToken);

        var scheduled = activeEnrollments
            .Where(x => x.Status == EnrollmentStatus.Enrolled)
            .Select(x => MapScheduledClass(x.CourseClass!))
            .ToList();
        var registered = activeEnrollments
            .OrderBy(x => x.Status == EnrollmentStatus.Waitlisted)
            .ThenBy(x => x.CourseClass!.DepartmentCode)
            .ThenBy(x => x.CourseClass!.CourseNumber)
            .ThenBy(x => x.CourseClass!.SessionCode)
            .Select(
                x => MapRegisteredClass(
                    x.CourseClass!,
                    x.Status,
                    x.WaitlistPosition,
                    enrolledCounts.GetValueOrDefault(x.CourseClassId)
                )
            )
            .ToList();

        return new CloudStudentScheduleDto
        {
            StudentId = BuildStudentToken(student),
            ScheduledClasses = scheduled,
            RegisteredClasses = registered,
            CurrentCredits = scheduled.Sum(x => x.Credits)
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

        var existingEnrollment = await dbContext.Enrollments.SingleOrDefaultAsync(
            x => x.StudentId == student.Id && x.CourseClassId == courseClass.Id,
            cancellationToken
        );

        if (existingEnrollment?.Status == EnrollmentStatus.Enrolled)
        {
            return (
                null,
                new RegistrationError(StatusCodes.Status409Conflict, "This class is already in your schedule.")
            );
        }

        if (existingEnrollment?.Status == EnrollmentStatus.Waitlisted)
        {
            return (
                null,
                new RegistrationError(StatusCodes.Status409Conflict, "You are already waitlisted for this class.")
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

        var unmetPrerequisites = await GetUnmetPrerequisitesAsync(student.Id, courseClass, null, cancellationToken);
        if (unmetPrerequisites.Count > 0)
        {
            return (
                null,
                new RegistrationError(
                    StatusCodes.Status403Forbidden,
                    $"Missing prerequisites: {string.Join(", ", unmetPrerequisites)}."
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

        var enrolledCount = await dbContext.Enrollments.CountAsync(
            x => x.CourseClassId == courseClass.Id && x.Status == EnrollmentStatus.Enrolled,
            cancellationToken
        );

        if (enrolledCount >= courseClass.Capacity)
        {
            var nextWaitlistPosition =
                await dbContext.Enrollments
                    .Where(x => x.CourseClassId == courseClass.Id && x.Status == EnrollmentStatus.Waitlisted)
                    .MaxAsync(x => (int?)x.WaitlistPosition, cancellationToken) ?? 0;

            if (existingEnrollment is null)
            {
                dbContext.Enrollments.Add(
                    new Enrollment
                    {
                        StudentId = student.Id,
                        CourseClassId = courseClass.Id,
                        Status = EnrollmentStatus.Waitlisted,
                        WaitlistPosition = nextWaitlistPosition + 1,
                        SourceSystem = ApplicationSource,
                        StatusChangedAtUtc = DateTimeOffset.UtcNow
                    }
                );
            }
            else
            {
                existingEnrollment.Status = EnrollmentStatus.Waitlisted;
                existingEnrollment.WaitlistPosition = nextWaitlistPosition + 1;
                existingEnrollment.SourceSystem = ApplicationSource;
                existingEnrollment.StatusChangedAtUtc = DateTimeOffset.UtcNow;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            var waitlistedSchedule = await GetStudentScheduleStateAsync(BuildStudentToken(student), cancellationToken);
            return (waitlistedSchedule, null);
        }

        if (existingEnrollment is null)
        {
            dbContext.Enrollments.Add(
                new Enrollment
                {
                    StudentId = student.Id,
                    CourseClassId = courseClass.Id,
                    Status = EnrollmentStatus.Enrolled,
                    SourceSystem = ApplicationSource,
                    StatusChangedAtUtc = DateTimeOffset.UtcNow
                }
            );
        }
        else
        {
            existingEnrollment.Status = EnrollmentStatus.Enrolled;
            existingEnrollment.WaitlistPosition = null;
            existingEnrollment.SourceSystem = ApplicationSource;
            existingEnrollment.StatusChangedAtUtc = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyEnrollmentChangeAsync(student, courseClass, "enrolled", cancellationToken);

        var schedule = await GetStudentScheduleStateAsync(BuildStudentToken(student), cancellationToken);
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

        if (enrollment is null || enrollment.Status == EnrollmentStatus.Dropped)
        {
            var untouched = await GetStudentScheduleStateAsync(BuildStudentToken(student), cancellationToken);
            return (untouched, null);
        }

        if (courseClass.DropDeadlineUtc.HasValue && courseClass.DropDeadlineUtc.Value < DateTimeOffset.UtcNow)
        {
            return (
                null,
                new RegistrationError(
                    StatusCodes.Status403Forbidden,
                    $"The drop deadline for {BuildExternalClassId(courseClass)} has passed."
                )
            );
        }

        var wasEnrolled = enrollment.Status == EnrollmentStatus.Enrolled;
        enrollment.Status = EnrollmentStatus.Dropped;
        enrollment.WaitlistPosition = null;
        enrollment.SourceSystem = ApplicationSource;
        enrollment.StatusChangedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        if (wasEnrolled)
        {
            var promoted = await PromoteWaitlistedStudentsAsync(courseClass.Id, cancellationToken);
            foreach (var item in promoted)
            {
                await NotifyEnrollmentChangeAsync(item.Student, item.CourseClass, "enrolled", cancellationToken);
            }
        }

        await NotifyEnrollmentChangeAsync(student, courseClass, "dropped", cancellationToken);
        var schedule = await GetStudentScheduleStateAsync(BuildStudentToken(student), cancellationToken);
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

        var desiredCourseCodes = resolvedClasses.Select(x => x.CourseCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var courseClass in resolvedClasses)
        {
            var unmetPrerequisites = await GetUnmetPrerequisitesAsync(
                student.Id,
                courseClass,
                desiredCourseCodes,
                cancellationToken
            );
            if (unmetPrerequisites.Count > 0)
            {
                return (
                    null,
                    new RegistrationError(
                        StatusCodes.Status403Forbidden,
                        $"Missing prerequisites for {BuildExternalClassId(courseClass)}: {string.Join(", ", unmetPrerequisites)}."
                    )
                );
            }
        }

        var existingEnrollments = await dbContext.Enrollments
            .Where(x => x.StudentId == student.Id)
            .Include(x => x.CourseClass)
            .ThenInclude(x => x!.Instructor)
            .ToListAsync(cancellationToken);

        var existingActive = existingEnrollments
            .Where(x => x.Status == EnrollmentStatus.Enrolled || x.Status == EnrollmentStatus.Waitlisted)
            .ToList();
        var existingActiveIds = existingActive.Select(x => x.CourseClassId).ToHashSet();
        var desiredIds = resolvedClasses.Select(x => x.Id).ToHashSet();
        var enrollmentCounts = new Dictionary<int, int>();
        var waitlistPositions = new Dictionary<int, int>();

        var newlyRequestedClassIds = desiredIds.Where(id => !existingActiveIds.Contains(id)).ToList();
        if (newlyRequestedClassIds.Count > 0)
        {
            enrollmentCounts = await dbContext.Enrollments
                .AsNoTracking()
                .Where(x => newlyRequestedClassIds.Contains(x.CourseClassId) && x.Status == EnrollmentStatus.Enrolled)
                .GroupBy(x => x.CourseClassId)
                .Select(group => new { CourseClassId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(x => x.CourseClassId, x => x.Count, cancellationToken);
            waitlistPositions = await dbContext.Enrollments
                .AsNoTracking()
                .Where(x => newlyRequestedClassIds.Contains(x.CourseClassId) && x.Status == EnrollmentStatus.Waitlisted)
                .GroupBy(x => x.CourseClassId)
                .Select(
                    group => new
                    {
                        CourseClassId = group.Key,
                        Position = group.Max(item => item.WaitlistPosition) ?? 0
                    }
                )
                .ToDictionaryAsync(x => x.CourseClassId, x => x.Position, cancellationToken);
        }

        var enrollmentsToRemove = existingActive
            .Where(x => !desiredIds.Contains(x.CourseClassId))
            .Where(x => x.Status == EnrollmentStatus.Enrolled || x.Status == EnrollmentStatus.Waitlisted)
            .ToList();
        foreach (var enrollment in enrollmentsToRemove)
        {
            enrollment.Status = EnrollmentStatus.Dropped;
            enrollment.WaitlistPosition = null;
            enrollment.SourceSystem = ApplicationSource;
            enrollment.StatusChangedAtUtc = DateTimeOffset.UtcNow;
        }

        var enrollmentsToAdd = new List<CourseClass>();
        var waitlistedClasses = new List<CourseClass>();
        foreach (var courseClass in resolvedClasses.Where(x => !existingActiveIds.Contains(x.Id)))
        {
            var existingEnrollment = existingEnrollments.SingleOrDefault(x => x.CourseClassId == courseClass.Id);
            var enrolledCount = enrollmentCounts.GetValueOrDefault(courseClass.Id, 0);
            var canEnrollImmediately = enrolledCount < courseClass.Capacity;
            var nextWaitlistPosition = waitlistPositions.GetValueOrDefault(courseClass.Id, 0) + 1;
            if (existingEnrollment is null)
            {
                dbContext.Enrollments.Add(
                    new Enrollment
                    {
                        StudentId = student.Id,
                        CourseClassId = courseClass.Id,
                        Status = canEnrollImmediately ? EnrollmentStatus.Enrolled : EnrollmentStatus.Waitlisted,
                        WaitlistPosition = canEnrollImmediately ? null : nextWaitlistPosition,
                        SourceSystem = ApplicationSource,
                        StatusChangedAtUtc = DateTimeOffset.UtcNow
                    }
                );
            }
            else
            {
                existingEnrollment.Status = canEnrollImmediately ? EnrollmentStatus.Enrolled : EnrollmentStatus.Waitlisted;
                existingEnrollment.WaitlistPosition = canEnrollImmediately ? null : nextWaitlistPosition;
                existingEnrollment.SourceSystem = ApplicationSource;
                existingEnrollment.StatusChangedAtUtc = DateTimeOffset.UtcNow;
            }

            if (canEnrollImmediately)
            {
                enrollmentsToAdd.Add(courseClass);
                enrollmentCounts[courseClass.Id] = enrolledCount + 1;
                continue;
            }

            waitlistedClasses.Add(courseClass);
            waitlistPositions[courseClass.Id] = nextWaitlistPosition;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var removedClass in enrollmentsToRemove.Select(x => x.CourseClass).Where(x => x is not null))
        {
            if (removedClass is not null)
            {
                var promoted = await PromoteWaitlistedStudentsAsync(removedClass.Id, cancellationToken);
                foreach (var item in promoted)
                {
                    await NotifyEnrollmentChangeAsync(item.Student, item.CourseClass, "enrolled", cancellationToken);
                }
            }
        }

        foreach (var addedClass in enrollmentsToAdd)
        {
            await NotifyEnrollmentChangeAsync(student, addedClass, "enrolled", cancellationToken);
        }

        foreach (var waitlistedClass in waitlistedClasses)
        {
            logger.LogInformation(
                "Student {StudentId} was placed on the waitlist for class {ClassId} at {TimestampUtc}.",
                BuildStudentToken(student),
                BuildExternalClassId(waitlistedClass),
                DateTimeOffset.UtcNow
            );
        }

        foreach (var removedClass in enrollmentsToRemove.Select(x => x.CourseClass).Where(x => x is not null))
        {
            await NotifyEnrollmentChangeAsync(student, removedClass!, "dropped", cancellationToken);
        }

        var schedule = await GetStudentScheduleStateAsync(BuildStudentToken(student), cancellationToken);
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
            .Include(x => x.Prerequisites)
            .OrderBy(x => x.DepartmentCode)
            .ThenBy(x => x.CourseNumber)
            .ThenBy(x => x.SessionCode)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return classes.Select(item => MapCloudClass(item)).ToList();
    }

    public async Task<CloudTeacherCatalogPageDto> GetTeacherCatalogAsync(
        string? search,
        string? department,
        string? studentToken,
        CancellationToken cancellationToken = default
    )
    {
        var query = dbContext.Instructors
            .AsNoTracking()
            .Include(x => x.Classes)
            .ThenInclude(x => x.Enrollments)
            .Include(x => x.Classes)
            .ThenInclude(x => x.Prerequisites)
            .AsQueryable();

        var normalizedDepartment = string.IsNullOrWhiteSpace(department) ? null : department.Trim();
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        if (normalizedDepartment is not null)
        {
            query = query.Where(
                instructor =>
                    instructor.Classes.Any(
                        courseClass =>
                            courseClass.DepartmentCode == normalizedDepartment
                            || courseClass.Department == normalizedDepartment
                            || EF.Functions.Like(courseClass.Department, $"%{normalizedDepartment}%")
                    )
            );
        }

        if (normalizedSearch is not null)
        {
            query = query.Where(
                instructor =>
                    EF.Functions.Like(instructor.FirstName, $"%{normalizedSearch}%")
                    || EF.Functions.Like(instructor.LastName, $"%{normalizedSearch}%")
                    || EF.Functions.Like(instructor.Email, $"%{normalizedSearch}%")
                    || instructor.Classes.Any(
                        courseClass =>
                            EF.Functions.Like(courseClass.CourseCode, $"%{normalizedSearch}%")
                            || EF.Functions.Like(courseClass.ClassName, $"%{normalizedSearch}%")
                            || EF.Functions.Like(courseClass.Department, $"%{normalizedSearch}%")
                    )
            );
        }

        var teachers = await query
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ToListAsync(cancellationToken);

        var courseClassIds = teachers.SelectMany(x => x.Classes.Select(courseClass => courseClass.Id)).Distinct();
        var studentStatuses = await BuildStudentEnrollmentLookupAsync(studentToken, courseClassIds, cancellationToken);
        var departments = await dbContext.CourseClasses
            .AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.Department))
            .Select(x => x.Department)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        return new CloudTeacherCatalogPageDto
        {
            Teachers = teachers
                .Select(
                    instructor =>
                    {
                        var teacherMatchesSearch =
                            normalizedSearch is not null
                            && (
                                instructor.FirstName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                                || instructor.LastName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                                || instructor.Email.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                            );

                        var visibleClasses = instructor.Classes
                            .Where(
                                courseClass =>
                                    MatchesTeacherDepartmentFilter(courseClass, normalizedDepartment)
                                    && MatchesTeacherSearchFilter(courseClass, normalizedSearch, teacherMatchesSearch)
                            )
                            .OrderBy(courseClass => courseClass.DepartmentCode)
                            .ThenBy(courseClass => courseClass.CourseNumber)
                            .ThenBy(courseClass => courseClass.SessionCode)
                            .ToList();

                        var classesForDisplay = visibleClasses.Count > 0 ? visibleClasses : instructor.Classes
                            .OrderBy(courseClass => courseClass.DepartmentCode)
                            .ThenBy(courseClass => courseClass.CourseNumber)
                            .ThenBy(courseClass => courseClass.SessionCode)
                            .ToList();

                        return new CloudTeacherCatalogDto
                        {
                            TeacherId = !string.IsNullOrWhiteSpace(instructor.ExternalId)
                                ? instructor.ExternalId
                                : instructor.Id.ToString(CultureInfo.InvariantCulture),
                            ExternalId = instructor.ExternalId ?? string.Empty,
                            Name = $"{instructor.FirstName} {instructor.LastName}",
                            Email = instructor.Email,
                            Department = ResolvePrimaryDepartment(classesForDisplay),
                            Classes = classesForDisplay
                                .Select(
                                    courseClass => MapCloudClass(
                                        courseClass,
                                        studentStatuses.GetValueOrDefault(courseClass.Id)
                                    )
                                )
                                .ToList()
                        };
                    }
                )
                .ToList(),
            Departments = departments,
            Total = teachers.Count
        };
    }

    public async Task<(CloudTeacherRosterDto? Roster, RegistrationError? Error)> GetTeacherRosterAsync(
        string teacherToken,
        string classToken,
        CancellationToken cancellationToken = default
    )
    {
        var teacher = await ResolveInstructorAsync(teacherToken, cancellationToken);
        if (teacher is null)
        {
            return (null, new RegistrationError(StatusCodes.Status404NotFound, "Teacher was not found."));
        }

        var (classInfo, accessError) = await ValidateTeacherClassAccessAsync(
            teacher,
            classToken,
            cancellationToken
        );
        if (accessError is not null)
        {
            return (null, accessError);
        }

        var ownedClass = classInfo!;
        var enrollments = await dbContext.Enrollments
            .AsNoTracking()
            .Where(x => x.CourseClassId == ownedClass.Id && x.Status == EnrollmentStatus.Enrolled)
            .Include(x => x.Student)
            .OrderBy(x => x.Student!.LastName)
            .ThenBy(x => x.Student!.FirstName)
            .ToListAsync(cancellationToken);

        return (new CloudTeacherRosterDto
        {
            ClassInfo = MapCloudClass(ownedClass),
            Students = enrollments
                .Select(
                    x =>
                        new CloudTeacherStudentDto
                        {
                            StudentId = BuildStudentToken(x.Student!),
                            Name = $"{x.Student!.FirstName} {x.Student.LastName}",
                            Email = x.Student.Email,
                            EnrollmentDateUtc = x.StatusChangedAtUtc
                        }
                )
                .ToList()
        }, null);
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

        var (classInfo, accessError) = await ValidateTeacherClassAccessAsync(
            teacher,
            classToken,
            cancellationToken
        );
        if (accessError is not null)
        {
            return (null, accessError);
        }

        var ownedClass = classInfo!;
        var enrolledCount = await dbContext.Enrollments.CountAsync(
            x => x.CourseClassId == ownedClass.Id && x.Status == EnrollmentStatus.Enrolled,
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

        ownedClass.Capacity = capacity;
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Teacher {TeacherId} updated capacity for class {ClassId} to {Capacity} at {TimestampUtc}.",
            teacherToken,
            BuildExternalClassId(ownedClass),
            capacity,
            DateTimeOffset.UtcNow
        );

        var promoted = await PromoteWaitlistedStudentsAsync(ownedClass.Id, cancellationToken);
        foreach (var item in promoted)
        {
            await NotifyEnrollmentChangeAsync(item.Student, item.CourseClass, "enrolled", cancellationToken);
        }

        ownedClass = await dbContext.CourseClasses
            .AsNoTracking()
            .Include(x => x.Instructor)
            .Include(x => x.Enrollments)
            .Include(x => x.Prerequisites)
            .SingleAsync(x => x.Id == ownedClass.Id, cancellationToken);

        return (MapCloudClass(ownedClass), null);
    }

    public async Task<(CloudClassDto? ClassInfo, RegistrationError? Error)> UpdateTeacherClassAsync(
        string teacherToken,
        string classToken,
        TeacherClassUpdateRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        var teacher = await ResolveInstructorAsync(teacherToken, cancellationToken);
        if (teacher is null)
        {
            return (null, new RegistrationError(StatusCodes.Status404NotFound, "Teacher was not found."));
        }

        var (classInfo, accessError) = await ValidateTeacherClassAccessAsync(
            teacher,
            classToken,
            cancellationToken
        );
        if (accessError is not null)
        {
            return (null, accessError);
        }

        var ownedClass = classInfo!;
        var title = request.Title.Trim();
        var location = request.Location.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return (
                null,
                new RegistrationError(StatusCodes.Status400BadRequest, "Class title is required.")
            );
        }

        if (string.IsNullOrWhiteSpace(location))
        {
            return (
                null,
                new RegistrationError(StatusCodes.Status400BadRequest, "Class location is required.")
            );
        }

        var normalizedDays = NormalizeDays(request.Days);
        if (normalizedDays.Count == 0)
        {
            return (
                null,
                new RegistrationError(
                    StatusCodes.Status400BadRequest,
                    "Select at least one meeting day."
                )
            );
        }

        if (
            !TryParseTime(request.StartTime, out var startTime)
            || !TryParseTime(request.EndTime, out var endTime)
        )
        {
            return (
                null,
                new RegistrationError(
                    StatusCodes.Status400BadRequest,
                    "Enter valid start and end times in HH:mm format."
                )
            );
        }

        if (endTime <= startTime)
        {
            return (
                null,
                new RegistrationError(
                    StatusCodes.Status400BadRequest,
                    "End time must be later than start time."
                )
            );
        }

        var enrolledCount = await dbContext.Enrollments.CountAsync(
            x => x.CourseClassId == ownedClass.Id && x.Status == EnrollmentStatus.Enrolled,
            cancellationToken
        );

        if (request.Capacity < enrolledCount)
        {
            return (
                null,
                new RegistrationError(
                    StatusCodes.Status400BadRequest,
                    $"Capacity cannot be lower than enrolled ({enrolledCount})."
                )
            );
        }

        ownedClass.ClassName = title;
        ownedClass.Location = location;
        ownedClass.Capacity = request.Capacity;
        ownedClass.DaysOfWeek = string.Join(',', normalizedDays);
        ownedClass.StartTime = startTime;
        ownedClass.EndTime = endTime;
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Teacher {TeacherId} updated class {ClassId} at {TimestampUtc}.",
            teacherToken,
            BuildExternalClassId(ownedClass),
            DateTimeOffset.UtcNow
        );

        var promoted = await PromoteWaitlistedStudentsAsync(ownedClass.Id, cancellationToken);
        foreach (var item in promoted)
        {
            await NotifyEnrollmentChangeAsync(item.Student, item.CourseClass, "enrolled", cancellationToken);
        }

        ownedClass = await dbContext.CourseClasses
            .AsNoTracking()
            .Include(x => x.Instructor)
            .Include(x => x.Enrollments)
            .Include(x => x.Prerequisites)
            .SingleAsync(x => x.Id == ownedClass.Id, cancellationToken);

        return (MapCloudClass(ownedClass), null);
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

        var (classInfo, accessError) = await ValidateTeacherClassAccessAsync(
            teacher,
            classToken,
            cancellationToken
        );
        if (accessError is not null)
        {
            return accessError;
        }

        var ownedClass = classInfo!;
        var student = await ResolveStudentAsync(studentToken, cancellationToken);
        if (student is null)
        {
            return new RegistrationError(StatusCodes.Status404NotFound, "Student was not found.");
        }

        var enrollment = await dbContext.Enrollments.SingleOrDefaultAsync(
            x => x.StudentId == student.Id && x.CourseClassId == ownedClass.Id,
            cancellationToken
        );

        if (enrollment is null || enrollment.Status == EnrollmentStatus.Dropped)
        {
            return null;
        }

        var wasEnrolled = enrollment.Status == EnrollmentStatus.Enrolled;
        enrollment.Status = EnrollmentStatus.Dropped;
        enrollment.WaitlistPosition = null;
        enrollment.SourceSystem = ApplicationSource;
        enrollment.StatusChangedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        if (wasEnrolled)
        {
            var promoted = await PromoteWaitlistedStudentsAsync(ownedClass.Id, cancellationToken);
            foreach (var item in promoted)
            {
                await NotifyEnrollmentChangeAsync(item.Student, item.CourseClass, "enrolled", cancellationToken);
            }
        }

        await NotifyEnrollmentChangeAsync(student, ownedClass, "dropped", cancellationToken);
        logger.LogInformation(
            "Teacher {TeacherId} removed student {StudentId} from class {ClassId} at {TimestampUtc}.",
            teacherToken,
            BuildStudentToken(student),
            BuildExternalClassId(ownedClass),
            DateTimeOffset.UtcNow
        );
        return null;
    }

    private async Task<IReadOnlyDictionary<int, StudentEnrollmentInfo>> BuildStudentEnrollmentLookupAsync(
        string? studentToken,
        IEnumerable<int> courseClassIds,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(studentToken))
        {
            return new Dictionary<int, StudentEnrollmentInfo>();
        }

        var student = await ResolveStudentAsync(studentToken, cancellationToken);
        if (student is null)
        {
            return new Dictionary<int, StudentEnrollmentInfo>();
        }

        var ids = courseClassIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, StudentEnrollmentInfo>();
        }

        return await dbContext.Enrollments
            .AsNoTracking()
            .Where(
                x =>
                    x.StudentId == student.Id
                    && ids.Contains(x.CourseClassId)
                    && x.Status != EnrollmentStatus.Dropped
            )
            .ToDictionaryAsync(
                x => x.CourseClassId,
                x => new StudentEnrollmentInfo(x.Status, x.WaitlistPosition),
                cancellationToken
            );
    }

    private async Task<(CourseClass? ClassInfo, RegistrationError? Error)> ValidateTeacherClassAccessAsync(
        Instructor teacher,
        string classToken,
        CancellationToken cancellationToken
    )
    {
        var classInfo = await ResolveClassAsync(classToken, null, cancellationToken);
        if (classInfo is null)
        {
            return (null, new RegistrationError(StatusCodes.Status404NotFound, "Class was not found."));
        }

        if (classInfo.InstructorId != teacher.Id)
        {
            logger.LogWarning(
                "Teacher {TeacherId} attempted to access class {ClassId} without ownership at {TimestampUtc}.",
                teacher.ExternalId ?? teacher.Id.ToString(CultureInfo.InvariantCulture),
                BuildExternalClassId(classInfo),
                DateTimeOffset.UtcNow
            );

            return (
                null,
                new RegistrationError(
                    StatusCodes.Status403Forbidden,
                    "You can only manage classes assigned to your instructor account."
                )
            );
        }

        return (classInfo, null);
    }

    private async Task<IReadOnlyList<string>> GetUnmetPrerequisitesAsync(
        int studentId,
        CourseClass courseClass,
        ISet<string>? additionalSatisfiedCourseCodes,
        CancellationToken cancellationToken
    )
    {
        var requiredCodes = await dbContext.CoursePrerequisites
            .AsNoTracking()
            .Where(x => x.CourseClassId == courseClass.Id)
            .Select(x => x.RequiredCourseCode)
            .ToListAsync(cancellationToken);

        if (requiredCodes.Count == 0)
        {
            return [];
        }

        var completedCodes = await dbContext.StudentCourseHistories
            .AsNoTracking()
            .Where(x => x.StudentId == studentId)
            .Select(x => x.CourseCode)
            .ToListAsync(cancellationToken);

        var currentCodes = await dbContext.Enrollments
            .AsNoTracking()
            .Where(
                x =>
                    x.StudentId == studentId
                    && x.Status == EnrollmentStatus.Enrolled
            )
            .Include(x => x.CourseClass)
            .Select(x => x.CourseClass!.CourseCode)
            .ToListAsync(cancellationToken);

        var satisfiedCodes = new HashSet<string>(completedCodes, StringComparer.OrdinalIgnoreCase);
        satisfiedCodes.UnionWith(currentCodes);
        if (additionalSatisfiedCourseCodes is not null)
        {
            satisfiedCodes.UnionWith(additionalSatisfiedCourseCodes);
        }

        return requiredCodes
            .Where(requiredCode => !satisfiedCodes.Contains(requiredCode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(requiredCode => requiredCode)
            .ToList();
    }

    private async Task<IReadOnlyList<(Student Student, CourseClass CourseClass)>> PromoteWaitlistedStudentsAsync(
        int courseClassId,
        CancellationToken cancellationToken
    )
    {
        var courseClass = await dbContext.CourseClasses
            .Include(x => x.Instructor)
            .SingleAsync(x => x.Id == courseClassId, cancellationToken);

        var enrollments = await dbContext.Enrollments
            .Where(
                x =>
                    x.CourseClassId == courseClassId
                    && (x.Status == EnrollmentStatus.Enrolled || x.Status == EnrollmentStatus.Waitlisted)
            )
            .Include(x => x.Student)
            .ToListAsync(cancellationToken);

        var enrolledCount = enrollments.Count(x => x.Status == EnrollmentStatus.Enrolled);
        var seatsAvailable = Math.Max(0, courseClass.Capacity - enrolledCount);
        if (seatsAvailable == 0)
        {
            return [];
        }

        var waitlisted = enrollments
            .Where(x => x.Status == EnrollmentStatus.Waitlisted)
            .OrderBy(x => x.WaitlistPosition ?? int.MaxValue)
            .ThenBy(x => x.StatusChangedAtUtc)
            .ToList();

        var promoted = new List<(Student Student, CourseClass CourseClass)>();
        foreach (var enrollment in waitlisted.Take(seatsAvailable))
        {
            enrollment.Status = EnrollmentStatus.Enrolled;
            enrollment.WaitlistPosition = null;
            enrollment.StatusChangedAtUtc = DateTimeOffset.UtcNow;

            if (enrollment.Student is not null)
            {
                promoted.Add((enrollment.Student, courseClass));
            }
        }

        var remainingWaitlist = waitlisted.Skip(seatsAvailable).ToList();
        for (var index = 0; index < remainingWaitlist.Count; index += 1)
        {
            remainingWaitlist[index].WaitlistPosition = index + 1;
        }

        if (promoted.Count > 0 || remainingWaitlist.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return promoted;
    }

    private async Task NotifyEnrollmentChangeAsync(
        Student student,
        CourseClass courseClass,
        string action,
        CancellationToken cancellationToken
    )
    {
        var instructorName = courseClass.Instructor is null
            ? "TBD"
            : $"{courseClass.Instructor.FirstName} {courseClass.Instructor.LastName}";
        var availableSeats = await dbContext.Enrollments.CountAsync(
            x => x.CourseClassId == courseClass.Id && x.Status == EnrollmentStatus.Enrolled,
            cancellationToken
        );

        await enrollmentNotificationService.SendEnrollmentReceiptAsync(
            new EnrollmentNotificationMessage(
                RecipientName: $"{student.FirstName} {student.LastName}".Trim(),
                RecipientEmail: student.Email,
                Action: action,
                StudentId: BuildStudentToken(student),
                ClassId: BuildExternalClassId(courseClass),
                ClassTitle: courseClass.ClassName,
                Department: !string.IsNullOrWhiteSpace(courseClass.Department)
                    ? courseClass.Department
                    : courseClass.DepartmentCode,
                Instructor: instructorName,
                Location: courseClass.Location,
                ScheduleSummary:
                    $"{courseClass.DaysOfWeek} {courseClass.StartTime:HH:mm}-{courseClass.EndTime:HH:mm}",
                Credits: courseClass.Credits,
                AvailableSeats: Math.Max(0, courseClass.Capacity - availableSeats),
                OccurredAtUtc: DateTimeOffset.UtcNow
            ),
            cancellationToken
        );
    }

    private async Task<Student?> ResolveStudentAsync(string studentToken, CancellationToken cancellationToken)
    {
        var token = studentToken.Trim();

        if (Guid.TryParse(token, out _))
        {
            var byExternalId = await dbContext.Students.SingleOrDefaultAsync(
                x => x.ExternalId == token,
                cancellationToken
            );
            if (byExternalId is not null)
            {
                return byExternalId;
            }
        }

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

        if (Guid.TryParse(token, out _))
        {
            var byExternalId = await dbContext.Instructors.SingleOrDefaultAsync(
                x => x.ExternalId == token,
                cancellationToken
            );
            if (byExternalId is not null)
            {
                return byExternalId;
            }
        }

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
            .Include(x => x.Prerequisites)
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

        if (Guid.TryParse(token, out _))
        {
            var byExternalId = await baseQuery.SingleOrDefaultAsync(x => x.ExternalId == token, cancellationToken);
            if (byExternalId is not null)
            {
                return byExternalId;
            }
        }

        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId))
        {
            return await baseQuery.SingleOrDefaultAsync(x => x.Id == numericId, cancellationToken);
        }

        if (token.Contains('-'))
        {
            var separatorIndex = token.LastIndexOf('-');
            var courseCodePart = token[..separatorIndex];
            var sectionPart = token[(separatorIndex + 1)..];
            var hasNumericSection = int.TryParse(
                sectionPart,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsedSectionId
            );

            var byComposite = await baseQuery
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(
                    x =>
                        x.CourseCode.ToLower() == courseCodePart.ToLower()
                        && (x.SessionCode.ToLower() == sectionPart.ToLower() || (hasNumericSection && x.Id == parsedSectionId)),
                    cancellationToken
                );

            if (byComposite is not null)
            {
                return byComposite;
            }

            token = courseCodePart;
        }

        return await baseQuery
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(x => x.CourseCode.ToLower() == token.ToLower(), cancellationToken);
    }

    private static CloudClassDto MapCloudClass(CourseClass source, StudentEnrollmentInfo? studentEnrollment = null)
    {
        var instructorName = source.Instructor is null
            ? "TBD"
            : $"{source.Instructor.FirstName} {source.Instructor.LastName}";
        var enrolledCount = source.Enrollments.Count(x => x.Status == EnrollmentStatus.Enrolled);

        return new CloudClassDto
        {
            SectionId = source.Id,
            Id = BuildExternalClassId(source),
            ExternalId = source.ExternalId ?? string.Empty,
            Title = source.ClassName,
            Department = source.Department,
            DepartmentCode = source.DepartmentCode,
            CourseNumber = source.CourseNumber,
            SessionCode = source.SessionCode,
            Instructor = instructorName,
            InstructorId = source.Instructor?.ExternalId ?? source.InstructorId.ToString(CultureInfo.InvariantCulture),
            Days = SplitDays(source.DaysOfWeek),
            StartTime = source.StartTime.ToString("HH:mm"),
            EndTime = source.EndTime.ToString("HH:mm"),
            Capacity = source.Capacity,
            EnrolledCount = enrolledCount,
            AvailableSeats = Math.Max(0, source.Capacity - enrolledCount),
            Credits = source.Credits,
            Room = source.Location,
            Location = source.Location,
            Term = !string.IsNullOrWhiteSpace(source.Semester) ? source.Semester : DefaultTerm,
            ColorHint = ResolveColorHint(source.DepartmentCode, source.CourseCode),
            IsStudentEnrolled = studentEnrollment?.Status == EnrollmentStatus.Enrolled,
            IsStudentWaitlisted = studentEnrollment?.Status == EnrollmentStatus.Waitlisted,
            EnrollmentStatus = studentEnrollment?.Status.ToString() ?? "NotEnrolled",
            StudentWaitlistPosition = studentEnrollment?.WaitlistPosition,
            Prerequisites = source.Prerequisites.Select(x => x.RequiredCourseCode).OrderBy(x => x).ToList(),
            DropDeadlineUtc = source.DropDeadlineUtc
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
            Term = !string.IsNullOrWhiteSpace(source.Semester) ? source.Semester : DefaultTerm,
            Days = SplitDays(source.DaysOfWeek),
            StartTime = source.StartTime.ToString("HH:mm"),
            EndTime = source.EndTime.ToString("HH:mm"),
            ColorHint = ResolveColorHint(source.DepartmentCode, source.CourseCode)
        };
    }

    private static CloudRegisteredClassDto MapRegisteredClass(
        CourseClass source,
        EnrollmentStatus status,
        int? waitlistPosition,
        int enrolledCount
    )
    {
        var scheduled = MapScheduledClass(source);

        return new CloudRegisteredClassDto
        {
            SectionId = scheduled.SectionId,
            ClassId = scheduled.ClassId,
            CourseCode = source.CourseCode,
            Title = scheduled.Title,
            Instructor = scheduled.Instructor,
            Credits = scheduled.Credits,
            Room = scheduled.Room,
            Location = scheduled.Location,
            Term = scheduled.Term,
            Days = scheduled.Days,
            StartTime = scheduled.StartTime,
            EndTime = scheduled.EndTime,
            ColorHint = scheduled.ColorHint,
            EnrollmentStatus = status.ToString(),
            WaitlistPosition = waitlistPosition,
            Capacity = source.Capacity,
            EnrolledCount = enrolledCount,
            AvailableSeats = Math.Max(0, source.Capacity - enrolledCount)
        };
    }

    private static string BuildExternalClassId(CourseClass source)
    {
        var sectionToken = !string.IsNullOrWhiteSpace(source.SessionCode)
            ? source.SessionCode
            : source.Id.ToString("00", CultureInfo.InvariantCulture);
        return $"{source.CourseCode}-{sectionToken}";
    }

    private static IReadOnlyList<string> SplitDays(string csv)
    {
        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Length > 0 ? char.ToUpperInvariant(x[0]) + x[1..].ToLowerInvariant() : x)
            .ToList();
    }

    private static IReadOnlyList<string> NormalizeDays(IEnumerable<string>? days)
    {
        return (days ?? [])
            .Select(day => day.Trim())
            .Where(day => !string.IsNullOrWhiteSpace(day))
            .Select(
                day =>
                    day.ToLowerInvariant() switch
                    {
                        "monday" or "mon" => "Mon",
                        "tuesday" or "tue" => "Tue",
                        "wednesday" or "wed" => "Wed",
                        "thursday" or "thu" => "Thu",
                        "friday" or "fri" => "Fri",
                        "saturday" or "sat" => "Sat",
                        "sunday" or "sun" => "Sun",
                        _ => string.Empty
                    }
            )
            .Where(day => !string.IsNullOrWhiteSpace(day))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryParseTime(string value, out TimeOnly time)
    {
        return TimeOnly.TryParseExact(
            value.Trim(),
            "HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out time
        );
    }

    private static string ResolveColorHint(string departmentCode, string courseCode)
    {
        if (
            departmentCode.StartsWith("MATH", StringComparison.OrdinalIgnoreCase)
            || courseCode.StartsWith("MATH", StringComparison.OrdinalIgnoreCase)
        )
        {
            return "red";
        }

        if (
            departmentCode.StartsWith("CSCE", StringComparison.OrdinalIgnoreCase)
            || courseCode.StartsWith("CSCE", StringComparison.OrdinalIgnoreCase)
        )
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
        var instructor = await dbContext.Instructors
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == instructorId, cancellationToken);
        if (instructor is null)
        {
            return instructorId.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(instructor.ExternalId))
        {
            return instructor.ExternalId;
        }

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

        if (!string.IsNullOrWhiteSpace(student.ExternalId))
        {
            return student.ExternalId;
        }

        return $"student-{student.Id}";
    }

    private static string ResolvePrimaryDepartment(IEnumerable<CourseClass> classes)
    {
        var selected = classes
            .Where(x => !string.IsNullOrWhiteSpace(x.Department))
            .GroupBy(x => x.Department)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => group.Key)
            .FirstOrDefault();

        return selected ?? "Unassigned";
    }

    private static bool MatchesTeacherDepartmentFilter(CourseClass courseClass, string? department)
    {
        if (string.IsNullOrWhiteSpace(department))
        {
            return true;
        }

        return string.Equals(courseClass.DepartmentCode, department, StringComparison.OrdinalIgnoreCase)
            || string.Equals(courseClass.Department, department, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(courseClass.Department)
                && courseClass.Department.Contains(department, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesTeacherSearchFilter(
        CourseClass courseClass,
        string? search,
        bool teacherMatchesSearch
    )
    {
        if (string.IsNullOrWhiteSpace(search) || teacherMatchesSearch)
        {
            return true;
        }

        return courseClass.CourseCode.Contains(search, StringComparison.OrdinalIgnoreCase)
            || courseClass.ClassName.Contains(search, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(courseClass.Department)
                && courseClass.Department.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesPassword(string storedPassword, string candidatePassword, string fallbackPassword)
    {
        return PasswordSecurity.VerifyPassword(storedPassword, candidatePassword, fallbackPassword);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
