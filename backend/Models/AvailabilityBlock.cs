namespace ClassFinder.Api.Models;

public class AvailabilityBlock
{
    public int Id { get; set; }
    public int StudentPreferenceId { get; set; }

    public StudentPreference? StudentPreference { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; } 

    public bool IsFlexible { get; set; } 
}