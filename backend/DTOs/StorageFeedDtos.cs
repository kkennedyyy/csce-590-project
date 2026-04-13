namespace ClassFinder.Api.DTOs;

public class StorageFeedEnvelopeDto
{
    public IReadOnlyList<StorageFeedStudentDto> Students { get; set; } = [];
    public IReadOnlyList<StorageFeedInstructorDto> Instructors { get; set; } = [];
    public IReadOnlyList<StorageFeedClassDto> Classes { get; set; } = [];
    public IReadOnlyList<StorageFeedEnrollmentDto> Enrollments { get; set; } = [];
}

public class StorageFeedStudentDto
{
    public string? StudentId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Major { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
}

public class StorageFeedInstructorDto
{
    public string? ProfessorId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public IReadOnlyList<string> ClassesTaught { get; set; } = [];
}

public class StorageFeedClassDto
{
    public int? SectionId { get; set; }
    public string? ClassId { get; set; }
    public string? ExternalId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public int? CourseNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public string SessionCode { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
    public string ProfessorId { get; set; } = string.Empty;
    public string InstructorEmail { get; set; } = string.Empty;
    public string InstructorFirstName { get; set; } = string.Empty;
    public string InstructorLastName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int Credits { get; set; }
    public int Capacity { get; set; }
    public string DaysOfWeek { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
}

public class StorageFeedEnrollmentDto
{
    public string? ExternalId { get; set; }
    public string? StudentId { get; set; }
    public string? StudentEmail { get; set; }
    public int? SectionId { get; set; }
    public string? ClassId { get; set; }
    public string? ExternalClassId { get; set; }
    public string Status { get; set; } = "Enrolled";
    public int? WaitlistPosition { get; set; }
    public DateTimeOffset? RecordedAtUtc { get; set; }
}
