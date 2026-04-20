namespace ClassFinder.Api.Models;

public class StudentPreference
{
    public int Id { get; set; }
    public int StudentId { get; set; }

    public Student? Student { get; set; }
    public string PreferredDaysOff { get; set; } = string.Empty;
    public int MinBreakMinutes {get; set; } = 15;
    public int FlexibilityLevel { get; set; } = 1;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AvailabilityBlock> AvailabilityBlocks {get; set; } = [];
    public ICollection<CourseRequirement> CourseRequirements { get; set; } = [];
    public ICollection<ElectivePreference> ElectivePreferences { get; set; } = [];


}