namespace ClassFinder.Api.Models;

public class CourseClass
{
    public int Id { get; set; }
    public string? ExternalId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public int? CourseNumber { get; set; }
    public string SessionCode { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int Credits { get; set; }
    public int Capacity { get; set; }
    public string DaysOfWeek { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public DateTimeOffset? DropDeadlineUtc { get; set; }
    public string Description { get; set; } = string.Empty;

    public int InstructorId { get; set; }
    public Instructor? Instructor { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Enrollment> Enrollments { get; set; } = [];
    public ICollection<CoursePrerequisite> Prerequisites { get; set; } = [];
}
