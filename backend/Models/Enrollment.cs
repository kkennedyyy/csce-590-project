namespace ClassFinder.Api.Models;

public class Enrollment
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }

    public int CourseClassId { get; set; }
    public CourseClass? CourseClass { get; set; }

    public EnrollmentStatus Status { get; set; }
    public int? WaitlistPosition { get; set; }
}
