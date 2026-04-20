namespace ClassFinder.Api.Models;

public class ElectivePreference
{
    public int Id { get; set; }
    public int StudentPreferenceId { get; set; }

    public StudentPreference? StudentPreference { get; set; }
    public string CourseCode { get; set; } = string.Empty;

    public int Priority { get; set; }
}