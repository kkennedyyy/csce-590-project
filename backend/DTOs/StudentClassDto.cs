namespace ClassFinder.Api.DTOs;

public class StudentClassDto
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string DaysTimes { get; set; } = string.Empty;
    public IReadOnlyList<string> Days { get; set; } = [];
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string Term { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int Credits { get; set; }
    public string Role { get; set; } = "general";
    public bool IsWaitlisted { get; set; }
    public int? WaitlistPosition { get; set; }
}
