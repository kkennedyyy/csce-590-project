namespace ClassFinder.Api.Models;

public class CourseClass
{
    public int Id { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int Credits { get; set; }
    public int Capacity { get; set; }
    public string DaysOfWeek { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public int InstructorId { get; set; }
    public Instructor? Instructor { get; set; }

    public ICollection<Enrollment> Enrollments { get; set; } = [];
}
