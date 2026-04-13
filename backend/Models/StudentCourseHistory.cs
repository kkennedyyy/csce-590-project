namespace ClassFinder.Api.Models;

public class StudentCourseHistory
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public DateTimeOffset CompletedAtUtc { get; set; }
}
