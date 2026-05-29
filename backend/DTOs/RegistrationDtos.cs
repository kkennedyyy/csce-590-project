namespace ClassFinder.Api.DTOs;

public class CloudClassPageDto
{
    public IReadOnlyList<CloudClassDto> Classes { get; set; } = [];
    public IReadOnlyList<string> Departments { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
    public int Total { get; set; }
}

public class CloudClassDto
{
    public int SectionId { get; set; }
    public string Id { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public int? CourseNumber { get; set; }
    public string SessionCode { get; set; } = string.Empty;
    public string Instructor { get; set; } = string.Empty;
    public string InstructorId { get; set; } = string.Empty;
    public IReadOnlyList<string> Days { get; set; } = [];
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int EnrolledCount { get; set; }
    public int AvailableSeats { get; set; }
    public int Credits { get; set; }
    public string Room { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Term { get; set; } = string.Empty;
    public string ColorHint { get; set; } = "neutral";
    public bool IsStudentEnrolled { get; set; }
    public bool IsStudentWaitlisted { get; set; }
    public string EnrollmentStatus { get; set; } = "NotEnrolled";
    public int? StudentWaitlistPosition { get; set; }
    public IReadOnlyList<string> Prerequisites { get; set; } = [];
    public DateTimeOffset? DropDeadlineUtc { get; set; }
}

public class CloudScheduledClassDto
{
    public int SectionId { get; set; }
    public string ClassId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Instructor { get; set; } = string.Empty;
    public int Credits { get; set; }
    public string Room { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Term { get; set; } = string.Empty;
    public IReadOnlyList<string> Days { get; set; } = [];
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string ColorHint { get; set; } = "neutral";
}

public class CloudRegisteredClassDto : CloudScheduledClassDto
{
    public string CourseCode { get; set; } = string.Empty;
    public string EnrollmentStatus { get; set; } = "Enrolled";
    public int? WaitlistPosition { get; set; }
    public int Capacity { get; set; }
    public int EnrolledCount { get; set; }
    public int AvailableSeats { get; set; }
}

public class CloudStudentScheduleDto
{
    public string StudentId { get; set; } = string.Empty;
    public IReadOnlyList<CloudScheduledClassDto> ScheduledClasses { get; set; } = [];
    public IReadOnlyList<CloudRegisteredClassDto> RegisteredClasses { get; set; } = [];
    public int CurrentCredits { get; set; }
}

public class CloudScheduleMutationRequestDto
{
    public int? SectionId { get; set; }
    public string? ClassId { get; set; }
}

public class CloudFinalizeScheduleRequestDto
{
    public IReadOnlyList<CloudFinalizeScheduleItemDto> ScheduledClasses { get; set; } = [];
}

public class CloudFinalizeScheduleItemDto
{
    public int? SectionId { get; set; }
    public string? ClassId { get; set; }
}

public class CloudTeacherStudentDto
{
    public string StudentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset? EnrollmentDateUtc { get; set; }
}

public class CloudTeacherRosterDto
{
    public CloudClassDto? ClassInfo { get; set; }
    public IReadOnlyList<CloudTeacherStudentDto> Students { get; set; } = [];
}

public class TeacherClassUpdateRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public IReadOnlyList<string> Days { get; set; } = [];
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
}

public class CloudTeacherCatalogPageDto
{
    public IReadOnlyList<CloudTeacherCatalogDto> Teachers { get; set; } = [];
    public IReadOnlyList<string> Departments { get; set; } = [];
    public int Total { get; set; }
}

public class CloudTeacherCatalogDto
{
    public string TeacherId { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public IReadOnlyList<CloudClassDto> Classes { get; set; } = [];
}

public class CloudAuthEnvelopeDto
{
    public CloudAuthUserDto User { get; set; } = new();
}

public class CloudAuthUserDto
{
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public sealed record RegistrationError(int StatusCode, string Message);
