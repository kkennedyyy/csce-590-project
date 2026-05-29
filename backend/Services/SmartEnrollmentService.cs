using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClassFinder.Api.Data;
using ClassFinder.Api.DTOs;
using ClassFinder.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClassFinder.Api.Services;

public class SmartEnrollmentService(
    ClassFinderDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IOptions<SmartEnrollmentOptions> options
) : ISmartEnrollmentService
{
    private const int MaxCredits = 19;
    private static readonly string[] DefaultBlockedDays = [];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex CourseCodeRegex = new(@"\b[A-Za-z]{3,4}\s?\d{3}\b", RegexOptions.Compiled);
    private static readonly Regex EarliestStartRegex = new(@"(?:after|start(?:ing)?\s+after|no\s+earlier\s+than)\s+(\d{1,2})(?::(\d{2}))?\s*(am|pm)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LatestEndRegex = new(@"(?:before|by|done\s+by|finish\s+by)\s+(\d{1,2})(?::(\d{2}))?\s*(am|pm)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BreakRegex = new(@"(\d{1,3})\s*(?:minute|min)\s+break", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ElectiveSlotRegex = new(@"(\d)\s+(?:preferred\s+)?elective", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Dictionary<string, string> DayTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["monday"] = "Mon",
        ["mon"] = "Mon",
        ["tuesday"] = "Tue",
        ["tue"] = "Tue",
        ["tues"] = "Tue",
        ["wednesday"] = "Wed",
        ["wed"] = "Wed",
        ["thursday"] = "Thu",
        ["thu"] = "Thu",
        ["thurs"] = "Thu",
        ["friday"] = "Fri",
        ["fri"] = "Fri"
    };

    public async Task<(SmartEnrollmentResponseDto? Response, RegistrationError? Error)> GenerateAsync(
        string studentToken,
        SmartEnrollmentRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        var student = await ResolveStudentAsync(studentToken, cancellationToken);
        if (student is null)
        {
            return (null, new RegistrationError(StatusCodes.Status404NotFound, "Student was not found."));
        }

        var activeStatuses = new[] { EnrollmentStatus.Enrolled, EnrollmentStatus.Waitlisted };
        var activeEnrollmentCourseIds = await dbContext.Enrollments
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id && activeStatuses.Contains(x.Status))
            .Select(x => x.CourseClassId)
            .ToListAsync(cancellationToken);
        var satisfiedCourseCodes = await dbContext.StudentCourseHistories
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id)
            .Select(x => x.CourseCode)
            .ToListAsync(cancellationToken);
        var activeCourseCodes = await dbContext.Enrollments
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id && x.Status == EnrollmentStatus.Enrolled)
            .Select(x => x.CourseClass!.CourseCode)
            .ToListAsync(cancellationToken);

        var catalog = await dbContext.CourseClasses
            .AsNoTracking()
            .Include(x => x.Instructor)
            .Include(x => x.Prerequisites)
            .Include(x => x.Enrollments)
            .Where(x => !activeEnrollmentCourseIds.Contains(x.Id))
            .OrderBy(x => x.DepartmentCode)
            .ThenBy(x => x.CourseNumber)
            .ThenBy(x => x.SessionCode)
            .ToListAsync(cancellationToken);

        var catalogOptions = catalog
            .Select(MapCatalogOption)
            .Where(x => x.AvailableSeats > 0)
            .ToList();

        var parsedPrompt = ParsePromptFallback(request.Prompt);
        var llmInterpretation = await InterpretPromptWithLlmAsync(request.Prompt, catalogOptions, cancellationToken);
        var preferences = MergeInterpretations(request.Prompt, parsedPrompt, llmInterpretation);
        var candidateLimit = Math.Clamp(
            request.CandidateLimit > 0 ? request.CandidateLimit : options.Value.DefaultCandidateLimit,
            1,
            6
        );

        var satisfiedCodes = new HashSet<string>(
            satisfiedCourseCodes.Concat(activeCourseCodes).Where(x => !string.IsNullOrWhiteSpace(x)),
            StringComparer.OrdinalIgnoreCase
        );

        preferences = ResolveCourseTargetsFromKeywords(preferences, catalogOptions);
        var candidates = GenerateCandidates(catalogOptions, preferences, satisfiedCodes, candidateLimit);

        var response = new SmartEnrollmentResponseDto
        {
            UsedLlm = llmInterpretation is not null,
            PlannerMode = llmInterpretation is not null ? "LLM + Rules" : "Rules",
            CatalogSize = catalogOptions.Count,
            Preferences = preferences,
            PreferenceSummary = BuildPreferenceSummary(preferences),
            Candidates = candidates
        };

        return (response, null);
    }

    private async Task<Student?> ResolveStudentAsync(string studentToken, CancellationToken cancellationToken)
    {
        var token = studentToken.Trim();

        if (Guid.TryParse(token, out _))
        {
            var byExternalId = await dbContext.Students.SingleOrDefaultAsync(
                x => x.ExternalId == token,
                cancellationToken
            );
            if (byExternalId is not null)
            {
                return byExternalId;
            }
        }

        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId))
        {
            return await dbContext.Students.SingleOrDefaultAsync(x => x.Id == numericId, cancellationToken);
        }

        if (token.Contains('@'))
        {
            var email = token.ToLowerInvariant();
            return await dbContext.Students.SingleOrDefaultAsync(x => x.Email.ToLower() == email, cancellationToken);
        }

        if (token.Equals("student-123", StringComparison.OrdinalIgnoreCase))
        {
            var demoStudent = await dbContext.Students.SingleOrDefaultAsync(
                x => x.Email.ToLower() == "john.smith@email.com",
                cancellationToken
            );

            if (demoStudent is not null)
            {
                return demoStudent;
            }
        }

        if (token.StartsWith("student-", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = token["student-".Length..];
            if (int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var suffixId))
            {
                var byId = await dbContext.Students.SingleOrDefaultAsync(x => x.Id == suffixId, cancellationToken);
                if (byId is not null)
                {
                    return byId;
                }
            }
        }

        return null;
    }

    private static CatalogOption MapCatalogOption(CourseClass source)
    {
        var enrolledCount = source.Enrollments.Count(x => x.Status == EnrollmentStatus.Enrolled);
        return new CatalogOption
        {
            SectionId = source.Id,
            ClassId = BuildExternalClassId(source),
            CourseCode = source.CourseCode,
            Title = source.ClassName,
            Department = source.Department,
            DepartmentCode = source.DepartmentCode,
            Description = source.Description,
            Instructor = source.Instructor is null
                ? "TBD"
                : $"{source.Instructor.FirstName} {source.Instructor.LastName}",
            Credits = source.Credits,
            Room = source.Location,
            Location = source.Location,
            Term = !string.IsNullOrWhiteSpace(source.Semester) ? source.Semester : "Fall 2026",
            ColorHint = ResolveColorHint(source.DepartmentCode, source.CourseCode),
            Days = SplitDays(source.DaysOfWeek),
            StartTime = source.StartTime.ToString("HH:mm"),
            EndTime = source.EndTime.ToString("HH:mm"),
            Capacity = source.Capacity,
            EnrolledCount = enrolledCount,
            AvailableSeats = Math.Max(0, source.Capacity - enrolledCount),
            Prerequisites = source.Prerequisites.Select(x => x.RequiredCourseCode).OrderBy(x => x).ToList()
        };
    }

    private async Task<PromptInterpretation?> InterpretPromptWithLlmAsync(
        string prompt,
        IReadOnlyList<CatalogOption> catalog,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(prompt) || !options.Value.IsLlmConfigured)
        {
            return null;
        }

        var client = httpClientFactory.CreateClient();
        var endpoint = $"{options.Value.LlmEndpoint.TrimEnd('/')}/openai/deployments/{options.Value.LlmDeployment}/chat/completions?api-version={options.Value.LlmApiVersion}";
        var catalogSummary = string.Join(
            "\n",
            catalog
                .Take(120)
                .Select(
                    item =>
                        $"{item.CourseCode} | {item.ClassId} | {item.Title} | {item.Department} | {string.Join('/', item.Days)} {item.StartTime}-{item.EndTime} | {item.Credits} credits | prereqs: {(item.Prerequisites.Count > 0 ? string.Join(", ", item.Prerequisites) : "none")}"
                )
        );

        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
        message.Headers.Add("api-key", options.Value.LlmApiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Content = JsonContent.Create(
            new
            {
                temperature = 0.1,
                max_tokens = 600,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content =
                            "You interpret a student's schedule-planning request into JSON for a deterministic scheduler. Return only JSON with keys: summary, requiredCourseCodes, preferredElectiveCourseCodes, requiredKeywords, preferredKeywords, electiveSlots, earliestStart, latestEnd, blockedDays, preferredNoClassDay, minimumBreakMinutes."
                    },
                    new
                    {
                        role = "user",
                        content = $"Student request:\n{prompt}\n\nCatalog excerpt:\n{catalogSummary}"
                    }
                }
            },
            options: JsonOptions
        );

        try
        {
            using var response = await client.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var content = payload.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            return DeserializePromptInterpretation(content);
        }
        catch
        {
            return null;
        }
    }

    private static SmartEnrollmentPreferencesDto MergeInterpretations(
        string prompt,
        PromptInterpretation fallback,
        PromptInterpretation? llmInterpretation
    )
    {
        var preferred = llmInterpretation ?? fallback;
        return new SmartEnrollmentPreferencesDto
        {
            Prompt = prompt.Trim(),
            RequiredCourseCodes = NormalizeDistinct(preferred.RequiredCourseCodes ?? []),
            PreferredElectiveCourseCodes = NormalizeDistinct(preferred.PreferredElectiveCourseCodes ?? []),
            RequiredKeywords = NormalizeDistinct(preferred.RequiredKeywords ?? []),
            PreferredKeywords = NormalizeDistinct(preferred.PreferredKeywords ?? []),
            ElectiveSlots = preferred.ElectiveSlots is > 0 ? preferred.ElectiveSlots.Value : 2,
            EarliestStart = NormalizeTime(preferred.EarliestStart) ?? "08:00",
            LatestEnd = NormalizeTime(preferred.LatestEnd) ?? "18:30",
            BlockedDays = NormalizeDistinct((preferred.BlockedDays ?? []).Select(MapDayToken)),
            PreferredNoClassDay = MapDayToken(preferred.PreferredNoClassDay),
            MinimumBreakMinutes = Math.Clamp(
                preferred.MinimumBreakMinutes is > 0 ? preferred.MinimumBreakMinutes.Value : 15,
                0,
                180
            ),
            Summary = !string.IsNullOrWhiteSpace(preferred.Summary)
                ? preferred.Summary.Trim()
                : "Prompt interpreted into ranked schedule constraints."
        };
    }

    private static PromptInterpretation? DeserializePromptInterpretation(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var lines = trimmed
                .Split('\n')
                .Where(line => !line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                .ToArray();
            trimmed = string.Join('\n', lines).Trim();
        }

        try
        {
            return JsonSerializer.Deserialize<PromptInterpretation>(trimmed, JsonOptions);
        }
        catch (JsonException)
        {
            var start = trimmed.IndexOf('{');
            var end = trimmed.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                return null;
            }

            var json = trimmed[start..(end + 1)];
            return JsonSerializer.Deserialize<PromptInterpretation>(json, JsonOptions);
        }
    }

    private static SmartEnrollmentPreferencesDto ResolveCourseTargetsFromKeywords(
        SmartEnrollmentPreferencesDto preferences,
        IReadOnlyList<CatalogOption> catalog
    )
    {
        var requiredCodes = new HashSet<string>(preferences.RequiredCourseCodes, StringComparer.OrdinalIgnoreCase);
        var preferredCodes = new HashSet<string>(preferences.PreferredElectiveCourseCodes, StringComparer.OrdinalIgnoreCase);

        foreach (var keyword in preferences.RequiredKeywords)
        {
            foreach (var code in MatchCourseCodesForKeyword(keyword, catalog).Take(2))
            {
                requiredCodes.Add(code);
            }
        }

        foreach (var keyword in preferences.PreferredKeywords)
        {
            foreach (var code in MatchCourseCodesForKeyword(keyword, catalog).Take(3))
            {
                if (!requiredCodes.Contains(code))
                {
                    preferredCodes.Add(code);
                }
            }
        }

        return new SmartEnrollmentPreferencesDto
        {
            Prompt = preferences.Prompt,
            RequiredCourseCodes = requiredCodes.OrderBy(x => x).ToList(),
            PreferredElectiveCourseCodes = preferredCodes.OrderBy(x => x).ToList(),
            RequiredKeywords = preferences.RequiredKeywords,
            PreferredKeywords = preferences.PreferredKeywords,
            ElectiveSlots = preferences.ElectiveSlots,
            EarliestStart = preferences.EarliestStart,
            LatestEnd = preferences.LatestEnd,
            BlockedDays = preferences.BlockedDays,
            PreferredNoClassDay = preferences.PreferredNoClassDay,
            MinimumBreakMinutes = preferences.MinimumBreakMinutes,
            Summary = preferences.Summary
        };
    }

    private static IReadOnlyList<SmartEnrollmentCandidateDto> GenerateCandidates(
        IReadOnlyList<CatalogOption> catalog,
        SmartEnrollmentPreferencesDto preferences,
        ISet<string> satisfiedCourseCodes,
        int limit
    )
    {
        var available = catalog
            .Where(item => item.Prerequisites.All(req => satisfiedCourseCodes.Contains(req)))
            .ToList();
        var grouped = available
            .GroupBy(item => item.CourseCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(x => x.AvailableSeats).ThenBy(x => x.ClassId).ToList(), StringComparer.OrdinalIgnoreCase);

        var requiredCourseCodes = NormalizeDistinct(preferences.RequiredCourseCodes);
        if (requiredCourseCodes.Any(code => !grouped.ContainsKey(code)))
        {
            return [];
        }

        var electiveCourseCodes = NormalizeDistinct(preferences.PreferredElectiveCourseCodes.Where(code => !requiredCourseCodes.Contains(code, StringComparer.OrdinalIgnoreCase)));
        var electivePlans = BuildElectivePlans(electiveCourseCodes, preferences.ElectiveSlots);
        if (requiredCourseCodes.Count == 0 && electiveCourseCodes.Count == 0)
        {
            electivePlans = BuildDefaultExplorationPlans(grouped.Keys.ToList(), limit);
        }

        var candidates = new List<CandidateDraft>();
        foreach (var electivePlan in electivePlans)
        {
            var plan = requiredCourseCodes
                .Concat(electivePlan)
                .Where(grouped.ContainsKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (plan.Count == 0)
            {
                continue;
            }

            var sectionsByCourse = plan.Select(code => grouped[code]).ToList();
            SearchSections(
                sectionsByCourse,
                preferences,
                electiveCourseCodes,
                0,
                [],
                candidates,
                limit
            );
        }

        return candidates
            .OrderBy(candidate => candidate.Score)
            .ThenBy(candidate => candidate.ScheduledClasses.Count)
            .ThenBy(candidate => candidate.ScheduledClasses.Sum(item => item.Credits))
            .Take(limit)
            .Select(
                (candidate, index) =>
                {
                    var totalCredits = candidate.ScheduledClasses.Sum(item => item.Credits);
                    var highlights = BuildHighlights(candidate.ScheduledClasses, preferences, totalCredits);
                    return new SmartEnrollmentCandidateDto
                    {
                        Id = $"candidate-{index + 1}",
                        ScheduledClasses = candidate.ScheduledClasses,
                        TotalCredits = totalCredits,
                        Summary = $"Option {index + 1} · {candidate.ScheduledClasses.Count} classes · {totalCredits} credits",
                        Rationale = BuildRationale(candidate.ScheduledClasses, preferences, highlights),
                        Highlights = highlights
                    };
                }
            )
            .ToList();
    }

    private static void SearchSections(
        IReadOnlyList<List<CatalogOption>> sectionsByCourse,
        SmartEnrollmentPreferencesDto preferences,
        IReadOnlyList<string> electiveCourseCodes,
        int courseIndex,
        List<CloudScheduledClassDto> current,
        List<CandidateDraft> candidates,
        int limit
    )
    {
        if (candidates.Count >= limit * 8)
        {
            return;
        }

        if (courseIndex >= sectionsByCourse.Count)
        {
            var score = ScoreCandidate(current, electiveCourseCodes, preferences);
            candidates.Add(new CandidateDraft(current.OrderBy(item => item.ClassId).ToList(), score));
            return;
        }

        foreach (var offering in sectionsByCourse[courseIndex])
        {
            var next = ToScheduledClass(offering);
            if (!FitsHardConstraints(current, next, preferences))
            {
                continue;
            }

            var nextCredits = current.Sum(item => item.Credits) + next.Credits;
            if (nextCredits > MaxCredits)
            {
                continue;
            }

            current.Add(next);
            SearchSections(sectionsByCourse, preferences, electiveCourseCodes, courseIndex + 1, current, candidates, limit);
            current.RemoveAt(current.Count - 1);
        }
    }

    private static bool FitsHardConstraints(
        IReadOnlyList<CloudScheduledClassDto> current,
        CloudScheduledClassDto candidate,
        SmartEnrollmentPreferencesDto preferences
    )
    {
        if (ToMinutes(candidate.StartTime) < ToMinutes(preferences.EarliestStart))
        {
            return false;
        }

        if (ToMinutes(candidate.EndTime) > ToMinutes(preferences.LatestEnd))
        {
            return false;
        }

        if (candidate.Days.Any(day => preferences.BlockedDays.Contains(day, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        foreach (var scheduled in current)
        {
            var sharedDays = scheduled.Days.Intersect(candidate.Days, StringComparer.OrdinalIgnoreCase).ToList();
            if (sharedDays.Count == 0)
            {
                continue;
            }

            var scheduledStart = ToMinutes(scheduled.StartTime);
            var scheduledEnd = ToMinutes(scheduled.EndTime);
            var candidateStart = ToMinutes(candidate.StartTime);
            var candidateEnd = ToMinutes(candidate.EndTime);

            if (candidateStart < scheduledEnd && scheduledStart < candidateEnd)
            {
                return false;
            }

            var gap = candidateStart >= scheduledEnd ? candidateStart - scheduledEnd : scheduledStart - candidateEnd;
            if (gap < preferences.MinimumBreakMinutes)
            {
                return false;
            }
        }

        return true;
    }

    private static int ScoreCandidate(
        IReadOnlyList<CloudScheduledClassDto> scheduledClasses,
        IReadOnlyList<string> electiveCourseCodes,
        SmartEnrollmentPreferencesDto preferences
    )
    {
        var rankedElectives = electiveCourseCodes.ToList();
        var offDayPenalty = !string.IsNullOrWhiteSpace(preferences.PreferredNoClassDay)
            && scheduledClasses.Any(item => item.Days.Contains(preferences.PreferredNoClassDay, StringComparer.OrdinalIgnoreCase))
            ? 40
            : 0;
        var electivePenalty = scheduledClasses.Aggregate(
            0,
            (score, item) =>
            {
                var courseCode = ExtractCourseCode(item.ClassId);
                var index = rankedElectives.IndexOf(courseCode);
                return index >= 0 ? score + index * 5 : score;
            }
        );
        var lateFinishPenalty = scheduledClasses.Max(item => ToMinutes(item.EndTime)) - ToMinutes(preferences.LatestEnd);

        return offDayPenalty + electivePenalty + Math.Max(0, lateFinishPenalty);
    }

    private static CloudScheduledClassDto ToScheduledClass(CatalogOption offering)
    {
        return new CloudScheduledClassDto
        {
            SectionId = offering.SectionId,
            ClassId = offering.ClassId,
            Title = offering.Title,
            Instructor = offering.Instructor,
            Credits = offering.Credits,
            Room = offering.Room,
            Location = offering.Location,
            Term = offering.Term,
            Days = offering.Days,
            StartTime = offering.StartTime,
            EndTime = offering.EndTime,
            ColorHint = offering.ColorHint
        };
    }

    private static IReadOnlyList<string> MatchCourseCodesForKeyword(string keyword, IReadOnlyList<CatalogOption> catalog)
    {
        var normalized = keyword.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        return catalog
            .Where(
                item =>
                    item.CourseCode.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                    || item.Title.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                    || item.Department.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                    || item.DepartmentCode.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                    || item.Description.Contains(normalized, StringComparison.OrdinalIgnoreCase)
            )
            .Select(item => item.CourseCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();
    }

    private static IReadOnlyList<string[]> BuildElectivePlans(IReadOnlyList<string> electiveCourseCodes, int electiveSlots)
    {
        var target = Math.Clamp(electiveSlots, 0, Math.Min(4, electiveCourseCodes.Count));
        if (target == 0 || electiveCourseCodes.Count == 0)
        {
            return [[]];
        }

        var plans = new List<string[]>();
        void Build(int index, List<string> current)
        {
            if (current.Count == target || index >= electiveCourseCodes.Count)
            {
                if (current.Count > 0)
                {
                    plans.Add(current.ToArray());
                }
                return;
            }

            Build(index + 1, [.. current, electiveCourseCodes[index]]);
            Build(index + 1, current);
        }

        Build(0, []);
        return plans.Count > 0 ? plans : [[]];
    }

    private static IReadOnlyList<string[]> BuildDefaultExplorationPlans(IReadOnlyList<string> courseCodes, int limit)
    {
        return courseCodes
            .Take(Math.Max(3, limit + 1))
            .Chunk(3)
            .Select(chunk => chunk.ToArray())
            .ToList();
    }

    private static IReadOnlyList<string> BuildPreferenceSummary(SmartEnrollmentPreferencesDto preferences)
    {
        var summary = new List<string>();
        if (!string.IsNullOrWhiteSpace(preferences.Summary))
        {
            summary.Add(preferences.Summary);
        }

        if (preferences.RequiredCourseCodes.Count > 0)
        {
            summary.Add($"Required: {string.Join(", ", preferences.RequiredCourseCodes)}");
        }

        if (preferences.PreferredElectiveCourseCodes.Count > 0)
        {
            summary.Add($"Preferred electives: {string.Join(", ", preferences.PreferredElectiveCourseCodes)}");
        }

        if (preferences.PreferredKeywords.Count > 0)
        {
            summary.Add($"Interests: {string.Join(", ", preferences.PreferredKeywords)}");
        }

        summary.Add($"Window: {preferences.EarliestStart}-{preferences.LatestEnd}");

        if (preferences.BlockedDays.Count > 0)
        {
            summary.Add($"Avoid: {string.Join(", ", preferences.BlockedDays)}");
        }

        if (!string.IsNullOrWhiteSpace(preferences.PreferredNoClassDay))
        {
            summary.Add($"Prefer {preferences.PreferredNoClassDay} off");
        }

        summary.Add($"Minimum break: {preferences.MinimumBreakMinutes} minutes");
        return summary;
    }

    private static IReadOnlyList<string> BuildHighlights(
        IReadOnlyList<CloudScheduledClassDto> scheduledClasses,
        SmartEnrollmentPreferencesDto preferences,
        int totalCredits
    )
    {
        var highlights = new List<string> { $"{totalCredits} total credits" };
        if (!string.IsNullOrWhiteSpace(preferences.PreferredNoClassDay)
            && scheduledClasses.All(item => !item.Days.Contains(preferences.PreferredNoClassDay, StringComparer.OrdinalIgnoreCase)))
        {
            highlights.Add($"Keeps {preferences.PreferredNoClassDay} open");
        }

        var earliest = scheduledClasses.Min(item => item.StartTime);
        var latest = scheduledClasses.Max(item => item.EndTime);
        highlights.Add($"Runs {earliest} to {latest}");

        if (preferences.MinimumBreakMinutes > 0)
        {
            highlights.Add($"Honors {preferences.MinimumBreakMinutes}-minute breaks");
        }

        return highlights;
    }

    private static string BuildRationale(
        IReadOnlyList<CloudScheduledClassDto> scheduledClasses,
        SmartEnrollmentPreferencesDto preferences,
        IReadOnlyList<string> highlights
    )
    {
        var codes = string.Join(", ", scheduledClasses.Select(item => item.ClassId));
        var summary = !string.IsNullOrWhiteSpace(preferences.Summary)
            ? preferences.Summary
            : "Balanced around your prompt.";
        return $"{summary} Includes {codes}. {string.Join(". ", highlights)}.";
    }

    private static PromptInterpretation ParsePromptFallback(string prompt)
    {
        var interpretation = new PromptInterpretation
        {
            Summary = string.IsNullOrWhiteSpace(prompt)
                ? "Use the text box to describe required classes, time windows, and free days."
                : "Prompt interpreted with the built-in parser."
        };

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return interpretation;
        }

        interpretation.RequiredCourseCodes = CourseCodeRegex
            .Matches(prompt)
            .Select(match => match.Value.Replace(" ", string.Empty).ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        interpretation.PreferredElectiveCourseCodes = interpretation.RequiredCourseCodes.Skip(1).ToList();

        var lowered = prompt.ToLowerInvariant();
        interpretation.BlockedDays ??= [];
        foreach (var token in DayTokens)
        {
            if (lowered.Contains($"no {token.Key}") || lowered.Contains($"avoid {token.Key}") || lowered.Contains($"can't do {token.Key}"))
            {
                interpretation.BlockedDays.Add(token.Value);
            }

            if (lowered.Contains($"free {token.Key}") || lowered.Contains($"{token.Key} off"))
            {
                interpretation.PreferredNoClassDay = token.Value;
            }
        }

        var earliestMatch = EarliestStartRegex.Match(prompt);
        if (earliestMatch.Success)
        {
            interpretation.EarliestStart = ConvertCapturedTime(earliestMatch.Groups[1].Value, earliestMatch.Groups[2].Value, earliestMatch.Groups[3].Value);
        }

        var latestMatch = LatestEndRegex.Match(prompt);
        if (latestMatch.Success)
        {
            interpretation.LatestEnd = ConvertCapturedTime(latestMatch.Groups[1].Value, latestMatch.Groups[2].Value, latestMatch.Groups[3].Value);
        }

        var breakMatch = BreakRegex.Match(prompt);
        if (breakMatch.Success && int.TryParse(breakMatch.Groups[1].Value, out var breakMinutes))
        {
            interpretation.MinimumBreakMinutes = breakMinutes;
        }

        var electiveMatch = ElectiveSlotRegex.Match(prompt);
        if (electiveMatch.Success && int.TryParse(electiveMatch.Groups[1].Value, out var electiveSlots))
        {
            interpretation.ElectiveSlots = electiveSlots;
        }

        interpretation.RequiredKeywords = InferKeywords(prompt, "need", "must take", "required").ToList();
        interpretation.PreferredKeywords = InferKeywords(prompt, "prefer", "interested in", "elective", "want").ToList();
        return interpretation;
    }

    private static string ConvertCapturedTime(string hourValue, string minuteValue, string meridiem)
    {
        var hour = int.Parse(hourValue, CultureInfo.InvariantCulture);
        var minute = string.IsNullOrWhiteSpace(minuteValue) ? 0 : int.Parse(minuteValue, CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(meridiem))
        {
            if (meridiem.Equals("pm", StringComparison.OrdinalIgnoreCase) && hour < 12)
            {
                hour += 12;
            }
            else if (meridiem.Equals("am", StringComparison.OrdinalIgnoreCase) && hour == 12)
            {
                hour = 0;
            }
        }

        return $"{hour:00}:{minute:00}";
    }

    private static IReadOnlyList<string> InferKeywords(string prompt, params string[] markers)
    {
        var lowered = prompt.ToLowerInvariant();
        var results = new List<string>();
        foreach (var marker in markers)
        {
            var index = lowered.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
            {
                continue;
            }

            var tail = prompt[(index + marker.Length)..].Trim();
            if (tail.Length == 0)
            {
                continue;
            }

            foreach (var chunk in tail.Split(new[] { ',', ';', '.', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(2))
            {
                if (chunk.Length >= 3 && !CourseCodeRegex.IsMatch(chunk))
                {
                    results.Add(chunk);
                }
            }
        }

        return NormalizeDistinct(results);
    }

    private static string BuildExternalClassId(CourseClass source)
    {
        var sectionToken = !string.IsNullOrWhiteSpace(source.SessionCode)
            ? source.SessionCode
            : source.Id.ToString("00", CultureInfo.InvariantCulture);
        return $"{source.CourseCode}-{sectionToken}";
    }

    private static List<string> SplitDays(string csv)
    {
        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(MapDayToken)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static string ResolveColorHint(string departmentCode, string courseCode)
    {
        return departmentCode.ToUpperInvariant() switch
        {
            "CSCE" => "red",
            "MATH" => "purple",
            _ => courseCode.StartsWith("CSCE", StringComparison.OrdinalIgnoreCase) ? "red" : "neutral"
        };
    }

    private static string MapDayToken(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        return DayTokens.TryGetValue(input.Trim(), out var mapped) ? mapped : input.Trim();
    }

    private static List<string> NormalizeDistinct(IEnumerable<string?> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NormalizeTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.ToString("HH:mm")
            : null;
    }

    private static int ToMinutes(string time)
    {
        var parsed = TimeOnly.ParseExact(time, "HH:mm", CultureInfo.InvariantCulture);
        return parsed.Hour * 60 + parsed.Minute;
    }

    private static string ExtractCourseCode(string classId)
    {
        var separator = classId.LastIndexOf('-');
        return separator > 0 ? classId[..separator] : classId;
    }

    private sealed class CatalogOption
    {
        public int SectionId { get; set; }
        public string ClassId { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string DepartmentCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Instructor { get; set; } = string.Empty;
        public int Credits { get; set; }
        public string Room { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Term { get; set; } = string.Empty;
        public string ColorHint { get; set; } = "neutral";
        public IReadOnlyList<string> Days { get; set; } = [];
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public int EnrolledCount { get; set; }
        public int AvailableSeats { get; set; }
        public IReadOnlyList<string> Prerequisites { get; set; } = [];
    }

    private sealed class PromptInterpretation
    {
        public string? Summary { get; set; } = string.Empty;
        public List<string>? RequiredCourseCodes { get; set; } = [];
        public List<string>? PreferredElectiveCourseCodes { get; set; } = [];
        public List<string>? RequiredKeywords { get; set; } = [];
        public List<string>? PreferredKeywords { get; set; } = [];
        public int? ElectiveSlots { get; set; } = 2;
        public string? EarliestStart { get; set; } = "08:00";
        public string? LatestEnd { get; set; } = "18:30";
        public List<string>? BlockedDays { get; set; } = [.. DefaultBlockedDays];
        public string? PreferredNoClassDay { get; set; } = string.Empty;
        public int? MinimumBreakMinutes { get; set; } = 15;
    }

    private sealed record CandidateDraft(IReadOnlyList<CloudScheduledClassDto> ScheduledClasses, int Score);
}
