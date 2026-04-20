namespace ClassFinder.Api.Services;

public class SmartEnrollmentOptions
{
    public const string SectionName = "SmartEnrollment";

    public string LlmEndpoint { get; set; } = string.Empty;
    public string LlmApiKey { get; set; } = string.Empty;
    public string LlmDeployment { get; set; } = string.Empty;
    public string LlmApiVersion { get; set; } = "2024-10-21";
    public int DefaultCandidateLimit { get; set; } = 4;

    public bool IsLlmConfigured =>
        !string.IsNullOrWhiteSpace(LlmEndpoint)
        && !string.IsNullOrWhiteSpace(LlmApiKey)
        && !string.IsNullOrWhiteSpace(LlmDeployment);
}
