namespace ClassFinder.Api.Models;

public class Student
{
    public int Id { get; set; }
    public string? ExternalId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Major { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Enrollment> Enrollments { get; set; } = [];
    public ICollection<StudentCourseHistory> CompletedCourses { get; set; } = [];
}
