using System.Globalization;
using System.Text.Json;
using ClassFinder.Api.Data;
using ClassFinder.Api.DTOs;
using ClassFinder.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClassFinder.Api.Services;

public sealed class FeedIngestionOptions
{
    public const string SectionName = "FeedIngestion";

    public bool Enabled { get; set; }
    public string WatchPath { get; set; } = Path.Combine("App_Data", "feeds");
    public string ProcessedPath { get; set; } = Path.Combine("App_Data", "feeds", "processed");
    public string FailedPath { get; set; } = Path.Combine("App_Data", "feeds", "failed");
}

public class StorageFeedImportService(
    ClassFinderDbContext dbContext,
    IOptions<FeedIngestionOptions> options,
    IHostEnvironment hostEnvironment,
    ILogger<StorageFeedImportService> logger
) : IStorageFeedImportService
{
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    public async Task ImportAsync(StorageFeedEnvelopeDto feed, CancellationToken cancellationToken = default)
    {
        var syncObservedAt = DateTimeOffset.UtcNow;

        var instructors = await dbContext.Instructors.ToListAsync(cancellationToken);
        var instructorsByEmail = instructors.ToDictionary(item => NormalizeKey(item.Email), item => item);
        var instructorsByExternalId = instructors
            .Where(item => !string.IsNullOrWhiteSpace(item.ExternalId))
            .ToDictionary(item => NormalizeKey(item.ExternalId), item => item);

        var students = await dbContext.Students.ToListAsync(cancellationToken);
        var studentsByEmail = students.ToDictionary(item => NormalizeKey(item.Email), item => item);
        var studentsByExternalId = students
            .Where(item => !string.IsNullOrWhiteSpace(item.ExternalId))
            .ToDictionary(item => NormalizeKey(item.ExternalId), item => item);

        foreach (var instructorFeed in feed.Instructors)
        {
            var instructor = ResolveInstructor(instructorsByExternalId, instructorsByEmail, instructorFeed);
            if (instructor is null)
            {
                continue;
            }

            if (instructor.Id == 0)
            {
                dbContext.Instructors.Add(instructor);
                instructors.Add(instructor);
            }

            ApplyInstructor(instructor, instructorFeed);
            TrackInstructor(instructor, instructorsByEmail, instructorsByExternalId);
        }

        foreach (var studentFeed in feed.Students)
        {
            var student = ResolveStudent(studentsByExternalId, studentsByEmail, studentFeed);
            if (student is null)
            {
                continue;
            }

            if (student.Id == 0)
            {
                dbContext.Students.Add(student);
                students.Add(student);
            }

            ApplyStudent(student, studentFeed);
            TrackStudent(student, studentsByEmail, studentsByExternalId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var courseClasses = await dbContext.CourseClasses.Include(item => item.Instructor).ToListAsync(cancellationToken);
        foreach (var classFeed in feed.Classes)
        {
            var instructor = await ResolveOrCreateInstructorAsync(
                classFeed.ProfessorId,
                classFeed.InstructorEmail,
                classFeed.InstructorFirstName,
                classFeed.InstructorLastName,
                string.Empty,
                instructorsByEmail,
                instructorsByExternalId,
                cancellationToken
            );

            if (instructor is null)
            {
                logger.LogWarning(
                    "Skipped class feed item {CourseCode} because its instructor could not be resolved.",
                    classFeed.CourseCode
                );
                continue;
            }

            var existing = MatchCourseClass(courseClasses, classFeed, instructor.Email);
            if (existing is null)
            {
                existing = new CourseClass();
                courseClasses.Add(existing);
                dbContext.CourseClasses.Add(existing);
            }

            ApplyCourseClass(existing, classFeed, instructor);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        courseClasses = await dbContext.CourseClasses
            .Include(item => item.Instructor)
            .Include(item => item.Enrollments)
            .ToListAsync(cancellationToken);
        var enrollments = await dbContext.Enrollments.ToListAsync(cancellationToken);

        foreach (var enrollmentFeed in feed.Enrollments)
        {
            var student = ResolveStudent(studentsByExternalId, studentsByEmail, enrollmentFeed.StudentId, enrollmentFeed.StudentEmail);
            var courseClass = MatchCourseClass(
                courseClasses,
                enrollmentFeed.SectionId,
                enrollmentFeed.ClassId,
                enrollmentFeed.ExternalClassId
            );

            if (student is null || courseClass is null)
            {
                logger.LogWarning(
                    "Skipped enrollment feed item for student {StudentToken} and class {ClassToken}.",
                    enrollmentFeed.StudentEmail ?? enrollmentFeed.StudentId,
                    enrollmentFeed.ClassId ?? enrollmentFeed.ExternalClassId ?? enrollmentFeed.SectionId?.ToString(CultureInfo.InvariantCulture)
                );
                continue;
            }

            if (!TryParseEnrollmentStatus(enrollmentFeed.Status, out var status))
            {
                logger.LogWarning(
                    "Skipped enrollment feed item for student {StudentToken} because status {Status} is invalid.",
                    enrollmentFeed.StudentEmail ?? enrollmentFeed.StudentId,
                    enrollmentFeed.Status
                );
                continue;
            }

            var existingEnrollment = enrollments.SingleOrDefault(
                item => item.StudentId == student.Id && item.CourseClassId == courseClass.Id
            );

            if (existingEnrollment is null)
            {
                existingEnrollment = new Enrollment
                {
                    StudentId = student.Id,
                    CourseClassId = courseClass.Id
                };
                dbContext.Enrollments.Add(existingEnrollment);
                enrollments.Add(existingEnrollment);
            }

            existingEnrollment.ExternalRecordId = enrollmentFeed.ExternalId;
            existingEnrollment.Status = status;
            existingEnrollment.WaitlistPosition = status == EnrollmentStatus.Waitlisted
                ? enrollmentFeed.WaitlistPosition
                : null;
            existingEnrollment.SourceSystem = "ExternalSync";
            existingEnrollment.StatusChangedAtUtc = enrollmentFeed.RecordedAtUtc ?? syncObservedAt;
            existingEnrollment.LastSeenInExternalSyncUtc = syncObservedAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ProcessFileAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var processedDirectory = ResolvePath(options.Value.ProcessedPath);
        var failedDirectory = ResolvePath(options.Value.FailedPath);

        try
        {
            var rawJson = await ReadFileWhenReadyAsync(path, cancellationToken);
            var payload = JsonSerializer.Deserialize<StorageFeedEnvelopeDto>(rawJson, _serializerOptions)
                ?? new StorageFeedEnvelopeDto();

            await ImportAsync(payload, cancellationToken);
            MoveFile(path, processedDirectory);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process storage feed file {Path}.", path);
            MoveFile(path, failedDirectory);
            throw;
        }
    }

    private async Task<Instructor?> ResolveOrCreateInstructorAsync(
        string? externalId,
        string email,
        string firstName,
        string lastName,
        string password,
        IDictionary<string, Instructor> instructorsByEmail,
        IDictionary<string, Instructor> instructorsByExternalId,
        CancellationToken cancellationToken
    )
    {
        var instructor = ResolveInstructor(
            instructorsByExternalId,
            instructorsByEmail,
            new StorageFeedInstructorDto
            {
                ProfessorId = externalId,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                Password = password
            }
        );

        if (instructor is null)
        {
            return null;
        }

        if (instructor.Id == 0)
        {
            dbContext.Instructors.Add(instructor);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            instructor.Email = email.Trim();
        }
        if (!string.IsNullOrWhiteSpace(firstName))
        {
            instructor.FirstName = firstName.Trim();
        }
        if (!string.IsNullOrWhiteSpace(lastName))
        {
            instructor.LastName = lastName.Trim();
        }
        if (!string.IsNullOrWhiteSpace(password))
        {
            instructor.Password = password.Trim();
        }
        if (!string.IsNullOrWhiteSpace(externalId))
        {
            instructor.ExternalId = externalId.Trim();
        }

        TrackInstructor(instructor, instructorsByEmail, instructorsByExternalId);
        return instructor;
    }

    private static Instructor? ResolveInstructor(
        IDictionary<string, Instructor> instructorsByExternalId,
        IDictionary<string, Instructor> instructorsByEmail,
        StorageFeedInstructorDto feed
    )
    {
        var normalizedExternalId = NormalizeKey(feed.ProfessorId);
        if (!string.IsNullOrWhiteSpace(normalizedExternalId) && instructorsByExternalId.TryGetValue(normalizedExternalId, out var byExternalId))
        {
            return byExternalId;
        }

        var normalizedEmail = NormalizeKey(feed.Email);
        if (!string.IsNullOrWhiteSpace(normalizedEmail) && instructorsByEmail.TryGetValue(normalizedEmail, out var byEmail))
        {
            return byEmail;
        }

        if (string.IsNullOrWhiteSpace(normalizedExternalId) && string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return null;
        }

        return new Instructor();
    }

    private static Student? ResolveStudent(
        IDictionary<string, Student> studentsByExternalId,
        IDictionary<string, Student> studentsByEmail,
        StorageFeedStudentDto feed
    )
    {
        var normalizedExternalId = NormalizeKey(feed.StudentId);
        if (!string.IsNullOrWhiteSpace(normalizedExternalId) && studentsByExternalId.TryGetValue(normalizedExternalId, out var byExternalId))
        {
            return byExternalId;
        }

        var normalizedEmail = NormalizeKey(feed.Email);
        if (!string.IsNullOrWhiteSpace(normalizedEmail) && studentsByEmail.TryGetValue(normalizedEmail, out var byEmail))
        {
            return byEmail;
        }

        if (string.IsNullOrWhiteSpace(normalizedExternalId) && string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return null;
        }

        return new Student();
    }

    private static Student? ResolveStudent(
        IDictionary<string, Student> studentsByExternalId,
        IDictionary<string, Student> studentsByEmail,
        string? studentId,
        string? studentEmail
    )
    {
        var normalizedExternalId = NormalizeKey(studentId);
        if (!string.IsNullOrWhiteSpace(normalizedExternalId) && studentsByExternalId.TryGetValue(normalizedExternalId, out var byExternalId))
        {
            return byExternalId;
        }

        var normalizedEmail = NormalizeKey(studentEmail);
        if (!string.IsNullOrWhiteSpace(normalizedEmail) && studentsByEmail.TryGetValue(normalizedEmail, out var byEmail))
        {
            return byEmail;
        }

        return null;
    }

    private static void ApplyInstructor(Instructor instructor, StorageFeedInstructorDto feed)
    {
        instructor.ExternalId = string.IsNullOrWhiteSpace(feed.ProfessorId) ? instructor.ExternalId : feed.ProfessorId.Trim();
        instructor.Email = feed.Email.Trim();
        instructor.FirstName = feed.FirstName.Trim();
        instructor.LastName = feed.LastName.Trim();
        instructor.Password = feed.Password.Trim();
    }

    private static void ApplyStudent(Student student, StorageFeedStudentDto feed)
    {
        student.ExternalId = string.IsNullOrWhiteSpace(feed.StudentId) ? student.ExternalId : feed.StudentId.Trim();
        student.Email = feed.Email.Trim();
        student.FirstName = feed.FirstName.Trim();
        student.LastName = feed.LastName.Trim();
        student.Password = feed.Password.Trim();
        student.Major = feed.Major.Trim();
        student.Classification = feed.Classification.Trim();
    }

    private static void ApplyCourseClass(CourseClass courseClass, StorageFeedClassDto feed, Instructor instructor)
    {
        var courseCode = BuildCourseCode(feed);
        courseClass.ExternalId = FirstNonEmpty(feed.ExternalId, feed.ClassId, courseClass.ExternalId);
        courseClass.CourseCode = courseCode;
        courseClass.ClassName = feed.Title.Trim();
        courseClass.Department = FirstNonEmpty(feed.Department, ExtractDepartmentName(courseCode), courseClass.Department) ?? string.Empty;
        courseClass.DepartmentCode = FirstNonEmpty(feed.DepartmentCode, ExtractDepartmentCode(courseCode), courseClass.DepartmentCode) ?? string.Empty;
        courseClass.CourseNumber = feed.CourseNumber ?? ExtractCourseNumber(courseCode);
        courseClass.SessionCode = FirstNonEmpty(feed.SessionCode, ExtractSessionCode(feed.ClassId), courseClass.SessionCode) ?? string.Empty;
        courseClass.Semester = feed.Semester.Trim();
        courseClass.Location = feed.Location.Trim();
        courseClass.Credits = feed.Credits;
        courseClass.Capacity = feed.Capacity;
        courseClass.DaysOfWeek = NormalizeDays(feed.DaysOfWeek);
        courseClass.StartTime = ParseTime(feed.StartTime);
        courseClass.EndTime = ParseTime(feed.EndTime);
        courseClass.InstructorId = instructor.Id;
        courseClass.Instructor = instructor;

        if (!courseClass.DropDeadlineUtc.HasValue)
        {
            courseClass.DropDeadlineUtc = BuildDefaultDropDeadline(feed.Semester);
        }
    }

    private static void TrackInstructor(
        Instructor instructor,
        IDictionary<string, Instructor> instructorsByEmail,
        IDictionary<string, Instructor> instructorsByExternalId
    )
    {
        if (!string.IsNullOrWhiteSpace(instructor.Email))
        {
            instructorsByEmail[NormalizeKey(instructor.Email)] = instructor;
        }

        if (!string.IsNullOrWhiteSpace(instructor.ExternalId))
        {
            instructorsByExternalId[NormalizeKey(instructor.ExternalId)] = instructor;
        }
    }

    private static void TrackStudent(
        Student student,
        IDictionary<string, Student> studentsByEmail,
        IDictionary<string, Student> studentsByExternalId
    )
    {
        if (!string.IsNullOrWhiteSpace(student.Email))
        {
            studentsByEmail[NormalizeKey(student.Email)] = student;
        }

        if (!string.IsNullOrWhiteSpace(student.ExternalId))
        {
            studentsByExternalId[NormalizeKey(student.ExternalId)] = student;
        }
    }

    private static CourseClass? MatchCourseClass(
        IReadOnlyList<CourseClass> courseClasses,
        StorageFeedClassDto feed,
        string instructorEmail
    )
    {
        var byToken = MatchCourseClass(courseClasses, feed.SectionId, feed.ClassId, feed.ExternalId);
        if (byToken is not null)
        {
            return byToken;
        }

        var normalizedCourseCode = BuildCourseCode(feed);
        var normalizedInstructorEmail = NormalizeKey(instructorEmail);
        var normalizedDays = NormalizeDays(feed.DaysOfWeek);
        var startTime = ParseTime(feed.StartTime);
        var endTime = ParseTime(feed.EndTime);

        return courseClasses.FirstOrDefault(
            item =>
                item.CourseCode.Equals(normalizedCourseCode, StringComparison.OrdinalIgnoreCase)
                && item.DaysOfWeek.Equals(normalizedDays, StringComparison.OrdinalIgnoreCase)
                && item.StartTime == startTime
                && item.EndTime == endTime
                && item.Instructor is not null
                && NormalizeKey(item.Instructor.Email) == normalizedInstructorEmail
        );
    }

    private static CourseClass? MatchCourseClass(
        IReadOnlyList<CourseClass> courseClasses,
        int? sectionId,
        string? classId,
        string? externalClassId
    )
    {
        var normalizedExternalClassId = NormalizeKey(externalClassId);
        if (!string.IsNullOrWhiteSpace(normalizedExternalClassId))
        {
            var byExternalId = courseClasses.FirstOrDefault(item => NormalizeKey(item.ExternalId) == normalizedExternalClassId);
            if (byExternalId is not null)
            {
                return byExternalId;
            }
        }

        if (sectionId.HasValue)
        {
            var bySection = courseClasses.FirstOrDefault(item => item.Id == sectionId.Value);
            if (bySection is not null)
            {
                return bySection;
            }
        }

        if (string.IsNullOrWhiteSpace(classId))
        {
            return null;
        }

        var token = classId.Trim();
        var normalizedToken = NormalizeKey(token);
        var byClassExternalId = courseClasses.FirstOrDefault(item => NormalizeKey(item.ExternalId) == normalizedToken);
        if (byClassExternalId is not null)
        {
            return byClassExternalId;
        }

        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId))
        {
            return courseClasses.FirstOrDefault(item => item.Id == numericId);
        }

        if (token.Contains('-'))
        {
            var parts = token.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (
                parts.Length >= 2
                && int.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sectionFromToken)
            )
            {
                var bySection = courseClasses.FirstOrDefault(item => item.Id == sectionFromToken);
                if (bySection is not null)
                {
                    return bySection;
                }
            }

            token = parts[0];
        }

        return courseClasses
            .OrderBy(item => item.Id)
            .FirstOrDefault(item => item.CourseCode.Equals(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryParseEnrollmentStatus(string rawStatus, out EnrollmentStatus status)
    {
        status = rawStatus.Trim().Equals("Dropped", StringComparison.OrdinalIgnoreCase)
            ? EnrollmentStatus.Dropped
            : EnrollmentStatus.Enrolled;

        return Enum.TryParse(rawStatus.Trim(), true, out status)
            || rawStatus.Trim().Equals("Dropped", StringComparison.OrdinalIgnoreCase)
            || rawStatus.Trim().Equals("Removed", StringComparison.OrdinalIgnoreCase)
            || rawStatus.Trim().Equals("Deleted", StringComparison.OrdinalIgnoreCase);
    }

    private static TimeOnly ParseTime(string value)
    {
        return TimeOnly.ParseExact(value.Trim(), "HH:mm", CultureInfo.InvariantCulture);
    }

    private static string NormalizeDays(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (value.Contains(','))
        {
            return string.Join(
                ',',
                value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(MapDayToken)
            );
        }

        var normalized = value.Trim().ToUpperInvariant();
        var days = new List<string>();
        for (var index = 0; index < normalized.Length; index += 1)
        {
            var remaining = normalized[index..];
            if (remaining.StartsWith("TH", StringComparison.Ordinal))
            {
                days.Add("Thu");
                index += 1;
                continue;
            }

            days.Add(MapDayToken(normalized[index].ToString()));
        }

        return string.Join(',', days.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string MapDayToken(string value)
    {
        return value.Trim().ToUpperInvariant() switch
        {
            "M" => "Mon",
            "T" => "Tue",
            "W" => "Wed",
            "TH" => "Thu",
            "F" => "Fri",
            "S" => "Sat",
            "SU" => "Sun",
            "MON" => "Mon",
            "TUE" => "Tue",
            "WED" => "Wed",
            "THU" => "Thu",
            "FRI" => "Fri",
            "SAT" => "Sat",
            "SUN" => "Sun",
            _ => char.ToUpperInvariant(value.Trim()[0]) + value.Trim()[1..].ToLowerInvariant()
        };
    }

    private static string NormalizeKey(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static string BuildCourseCode(StorageFeedClassDto feed)
    {
        if (!string.IsNullOrWhiteSpace(feed.CourseCode))
        {
            return feed.CourseCode.Trim();
        }

        if (!string.IsNullOrWhiteSpace(feed.DepartmentCode) && feed.CourseNumber.HasValue)
        {
            return $"{feed.DepartmentCode.Trim().ToUpperInvariant()}{feed.CourseNumber.Value}";
        }

        return feed.ExternalId ?? feed.ClassId ?? "UNKNOWN";
    }

    private static string ExtractDepartmentCode(string courseCode)
    {
        return new string(courseCode.TakeWhile(char.IsLetter).ToArray());
    }

    private static int? ExtractCourseNumber(string courseCode)
    {
        var digits = new string(courseCode.SkipWhile(char.IsLetter).TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static string ExtractDepartmentName(string courseCode)
    {
        var code = ExtractDepartmentCode(courseCode);
        return code switch
        {
            "CSCE" => "Computer Science",
            "MATH" => "Mathematics",
            "ARTS" => "Art",
            "PHYS" => "Physics",
            "CHEM" => "Chemistry",
            "STAT" => "Statistics",
            "BIOL" => "Biology",
            "ECON" => "Economics",
            "HIST" => "History",
            "ENGL" => "English",
            "PSYC" => "Psychology",
            _ => code
        };
    }

    private static string? ExtractSessionCode(string? classId)
    {
        if (string.IsNullOrWhiteSpace(classId) || !classId.Contains('-'))
        {
            return null;
        }

        var parts = classId.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 1 ? parts[^1] : null;
    }

    private static DateTimeOffset? BuildDefaultDropDeadline(string semester)
    {
        if (string.IsNullOrWhiteSpace(semester))
        {
            return null;
        }

        var season = semester.Trim().ToLowerInvariant();
        var year = DateTimeOffset.UtcNow.Year;
        return season switch
        {
            "spring" => new DateTimeOffset(year, 2, 15, 23, 59, 59, TimeSpan.Zero),
            "summer" => new DateTimeOffset(year, 6, 15, 23, 59, 59, TimeSpan.Zero),
            "fall" => new DateTimeOffset(year, 9, 15, 23, 59, 59, TimeSpan.Zero),
            _ => null
        };
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    private async Task<string> ReadFileWhenReadyAsync(string path, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 10; attempt += 1)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync(cancellationToken);
            }
            catch (IOException) when (attempt < 10)
            {
                await Task.Delay(150, cancellationToken);
            }
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    private void MoveFile(string path, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(path));

        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        File.Move(path, destinationPath);
    }

    private string ResolvePath(string configuredPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(hostEnvironment.ContentRootPath, configuredPath);
    }
}
