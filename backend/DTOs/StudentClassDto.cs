namespace ClassFinder.Api.DTOs;

public class StudentClassDto
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string DaysTimes { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int Credits { get; set; }
    public bool IsWaitlisted { get; set; }
    public int? WaitlistPosition { get; set; }
}
