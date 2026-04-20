namespace ClassFinder.Api.DTOs;

public class GeneratedScheduleDto
{
    public int ScheduleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TotalCredits { get; set; }
    public List<StudentClassDto> Classes { get; set; } = [];
    public bool HasConflicts { get; set; }
    public string Notes { get; set; } = string.Empty;
}