namespace ClassFinder.Api.Models;

public class CourseRequirement
{
    public int Id { get; set; }
    public int StudentPreferenceId { get; set; }

    public StudentPreference? StudentPreference { get; set; }
    public string CourseCode { get; set; } = string.Empty;

    public bool IsRequired {get; set;}
    public bool IsFlexible { get; set; }
}