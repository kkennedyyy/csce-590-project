namespace ClassFinder.Api.DTOs;

public class ClassDetailDto
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string Professor { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int EnrolledCount { get; set; }
    public bool IsAtCapacity { get; set; }
    public int WaitlistCount { get; set; }
    public string Location { get; set; } = string.Empty;
    public string DaysOfWeek { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int Credits { get; set; }
    public IReadOnlyList<WaitlistEntryDto> WaitlistPositions { get; set; } = [];
}

public class WaitlistEntryDto
{
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int Position { get; set; }
}
