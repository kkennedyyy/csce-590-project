namespace ClassFinder.Api.DTOs;

public class SmartEnrollmentRequestDto
{
    public string Prompt { get; set; } = string.Empty;
    public int CandidateLimit { get; set; } = 4;
}

public class SmartEnrollmentPreferencesDto
{
    public string Prompt { get; set; } = string.Empty;
    public IReadOnlyList<string> RequiredCourseCodes { get; set; } = [];
    public IReadOnlyList<string> PreferredElectiveCourseCodes { get; set; } = [];
    public IReadOnlyList<string> RequiredKeywords { get; set; } = [];
    public IReadOnlyList<string> PreferredKeywords { get; set; } = [];
    public int ElectiveSlots { get; set; }
    public string EarliestStart { get; set; } = "08:00";
    public string LatestEnd { get; set; } = "18:30";
    public IReadOnlyList<string> BlockedDays { get; set; } = [];
    public string PreferredNoClassDay { get; set; } = string.Empty;
    public int MinimumBreakMinutes { get; set; } = 15;
    public string Summary { get; set; } = string.Empty;
}

public class SmartEnrollmentCandidateDto
{
    public string Id { get; set; } = string.Empty;
    public IReadOnlyList<CloudScheduledClassDto> ScheduledClasses { get; set; } = [];
    public int TotalCredits { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public IReadOnlyList<string> Highlights { get; set; } = [];
}

public class SmartEnrollmentResponseDto
{
    public bool UsedLlm { get; set; }
    public string PlannerMode { get; set; } = "Rules";
    public int CatalogSize { get; set; }
    public SmartEnrollmentPreferencesDto Preferences { get; set; } = new();
    public IReadOnlyList<string> PreferenceSummary { get; set; } = [];
    public IReadOnlyList<SmartEnrollmentCandidateDto> Candidates { get; set; } = [];
}
