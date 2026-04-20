using ClassFinder.Api.Models;


namespace ClassFinder.Api.DTOs;

public class ScheduleRequestDto
{
    public int StudentId { get; set; }
    public int MinBreakMinutes { get; set; } = 15;
    public int FlexibilityLevel { get; set; } = 1; // 1-5
    public int? MinCredits { get; set; } = 12;
    public int? MaxCredits { get; set; } = 18;
    /// <summary>Required class IDs (int). Also accepts the flat list from the smart enrollment form.</summary>
    public List<int> CourseRequirements { get; set; } = [];
    /// <summary>Flat list of required course IDs — alias for CourseRequirements sent by the frontend.</summary>
    public List<int> RequiredCourseIds
    {
        get => CourseRequirements;
        set => CourseRequirements = value;
    }
    public List<ElectivePreferenceDto> ElectivePreferences { get; set; } = [];
    /// <summary>Flat list of preferred elective class IDs sent by the frontend.</summary>
    public List<int> PreferredElectiveIds { get; set; } = [];
    public List<string> PreferredDaysOff { get; set; } = [];
    public bool PreferOnlineClasses { get; set; } = false;
    public bool AvoidEarlyClasses { get; set; } = false;
}

public class ElectivePreferenceDto
{
    public int ElectiveGroupId { get; set; }
    public int PreferredOptionId { get; set; } // Which elective to prioritize
}