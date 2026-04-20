namespace ClassFinder.Api.DTOs;

public class StudentScheduleStateDto
{
    public string StudentId { get; set; } = string.Empty;
    public IReadOnlyList<StudentScheduledClassDto> ScheduledClasses { get; set; } = [];
    public int CurrentCredits { get; set; }
}

public class StudentScheduledClassDto
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

public class StudentScheduleUpdateRequest
{
    public int? SectionId { get; set; }
    public string? ClassId { get; set; }
}
