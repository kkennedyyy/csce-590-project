using ClassFinder.Api.Data;
using ClassFinder.Api.DTOs;
using ClassFinder.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassFinder.Api.Services;

public class ScheduleGenerationService : IScheduleGenerationService
{
    private readonly ClassFinderDbContext _context;
    private readonly Random _random = new();
    private static readonly string[] PreferredDayOrder = ["Mon", "Tue", "Wed", "Thu", "Fri"];

    public ScheduleGenerationService(ClassFinderDbContext context)
    {
        _context = context;
    }

    public async Task<List<GeneratedScheduleDto>> GenerateSchedulesAsync(
        ScheduleRequestDto request, 
        CancellationToken cancellationToken)
    {
        var allClasses = await _context.CourseClasses
            .Include(c => c.Instructor)
            .ToListAsync(cancellationToken);

        if (allClasses.Count == 0)
        {
            return [];
        }

        var minCredits = Math.Clamp(request.MinCredits ?? 12, 0, 21);
        var maxCredits = Math.Clamp(request.MaxCredits ?? 18, minCredits, 21);
        var normalizedFlexibility = Math.Clamp(request.FlexibilityLevel, 1, 5);
        var preferredDaysOff = request.PreferredDaysOff
            .Select(d => d.Trim())
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var minBreak = Math.Max(0, request.MinBreakMinutes);

        var requiredIds = new HashSet<int>(request.CourseRequirements);
        var requiredClasses = allClasses
            .Where(c => requiredIds.Contains(c.Id))
            .ToList();

        var preferredElectiveIds = request.ElectivePreferences
            .Where(ep => ep.PreferredOptionId > 0)
            .Select(ep => ep.PreferredOptionId)
            .Concat(request.PreferredElectiveIds)
            .ToHashSet();

        var preferredElectiveClasses = allClasses
            .Where(c => preferredElectiveIds.Contains(c.Id))
            .ToList();

        var requiredSemesters = requiredClasses
            .Select(c => NormalizeSemester(c.Semester))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var electiveSemesters = preferredElectiveClasses
            .Select(c => NormalizeSemester(c.Semester))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requiredSemesters.Count > 1)
        {
            throw new InvalidOperationException(
                $"Required course selections span multiple semesters ({string.Join(", ", requiredSemesters)}). Choose required courses from a single semester."
            );
        }

        if (electiveSemesters.Count > 1)
        {
            throw new InvalidOperationException(
                $"Preferred electives span multiple semesters ({string.Join(", ", electiveSemesters)}). Choose electives from a single semester."
            );
        }

        if (requiredSemesters.Count == 1 && electiveSemesters.Count == 1
            && !string.Equals(requiredSemesters[0], electiveSemesters[0], StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Required courses are offered in {requiredSemesters[0]} but preferred electives are in {electiveSemesters[0]}. Adjust your selections so all courses are in the same semester."
            );
        }

        var targetSemester = requiredSemesters.FirstOrDefault()
            ?? electiveSemesters.FirstOrDefault()
            ?? allClasses
                .Select(c => NormalizeSemester(c.Semester))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .Select(group => group.Key)
                .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(targetSemester))
        {
            allClasses = allClasses
                .Where(c => string.Equals(NormalizeSemester(c.Semester), targetSemester, StringComparison.OrdinalIgnoreCase))
                .ToList();

            requiredClasses = requiredClasses
                .Where(c => string.Equals(NormalizeSemester(c.Semester), targetSemester, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (requiredIds.Count > 0 && requiredClasses.Count != requiredIds.Count)
            {
                throw new InvalidOperationException(
                    $"Some required courses are not offered in {targetSemester}. Update required selections to a single semester."
                );
            }

            preferredElectiveIds = preferredElectiveClasses
                .Where(c => string.Equals(NormalizeSemester(c.Semester), targetSemester, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Id)
                .ToHashSet();
        }

        var electiveClasses = allClasses.Where(c => !requiredIds.Contains(c.Id)).ToList();

        var requiredWarnings = BuildRequiredPreferenceWarnings(
            allClasses,
            requiredClasses,
            preferredDaysOff,
            request.AvoidEarlyClasses,
            request.PreferOnlineClasses
        );

        var requiredConflictCount = CountConflicts(requiredClasses, minBreak);

        var profiles = new[]
        {
            new OptionProfile(1, "Option 1 - Preference Strict", StrictnessBias: 0.9, PreferLaterStartsBias: 1.0, PreferOnlineBias: 1.0),
            new OptionProfile(2, "Option 2 - Balanced", StrictnessBias: 0.55, PreferLaterStartsBias: 0.5, PreferOnlineBias: 0.5),
            new OptionProfile(3, "Option 3 - Exploratory", StrictnessBias: 0.2, PreferLaterStartsBias: 0.1, PreferOnlineBias: 0.1)
        };

        var generatedSchedules = new List<GeneratedScheduleDto>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var profile in profiles)
        {
            var creditsWindow = ResolveTargetCredits(minCredits, maxCredits, normalizedFlexibility, profile.ScheduleId);
            var optionResult = BuildOption(
                allClasses,
                electiveClasses,
                requiredClasses,
                preferredElectiveIds,
                profile,
                normalizedFlexibility,
                preferredDaysOff,
                request.AvoidEarlyClasses,
                request.PreferOnlineClasses,
                minBreak,
                creditsWindow.Min,
                creditsWindow.Target,
                creditsWindow.Max
            );

            var option = optionResult.Schedule;

            if (option.Classes.Count == 0)
            {
                continue;
            }

            var key = string.Join('|', option.Classes.Select(c => c.ClassId).OrderBy(id => id));
            if (seenKeys.Contains(key))
            {
                continue;
            }

            seenKeys.Add(key);
            option.ScheduleId = profile.ScheduleId;
            option.Title = profile.Title;

            var notes = new List<string>
            {
                $"Credits target: {creditsWindow.Min}-{creditsWindow.Max}. Generated {option.TotalCredits} credits.",
                $"Flexibility applied: {normalizedFlexibility}/5 ({profile.Title})."
            };

            if (requiredConflictCount > 0)
            {
                notes.Add($"Required class conflicts detected: {requiredConflictCount}. Resolve conflicts manually.");
                option.HasConflicts = true;
            }

            notes.AddRange(requiredWarnings);
            notes.AddRange(optionResult.Warnings);
            option.Notes = string.Join("\n", notes.Distinct());

            generatedSchedules.Add(option);
        }

        if (generatedSchedules.Count == 0 && requiredClasses.Count > 0)
        {
            var fallbackClasses = requiredClasses
                .Select(course => MapToStudentClassDto(course, requiredClasses, preferredElectiveIds, []))
                .ToList();
            var fallbackCredits = fallbackClasses.Sum(c => c.Credits);
            return
            [
                new GeneratedScheduleDto
                {
                    ScheduleId = 1,
                    Title = "Required Classes Only",
                    TotalCredits = fallbackCredits,
                    Classes = fallbackClasses,
                    HasConflicts = requiredConflictCount > 0,
                    Notes = string.Join(
                        "\n",
                        new[]
                        {
                            "No complete schedule met all constraints.",
                            $"Required-only fallback generated with {fallbackCredits} credits."
                        }.Concat(requiredWarnings).Distinct()
                    )
                }
            ];
        }

        return generatedSchedules;
    }

    private OptionBuildResult BuildOption(
        List<CourseClass> allClasses,
        List<CourseClass> electiveClasses,
        List<CourseClass> requiredClasses,
        HashSet<int> preferredElectiveIds,
        OptionProfile profile,
        int flexibilityLevel,
        HashSet<string> preferredDaysOff,
        bool avoidEarlyClasses,
        bool preferOnlineClasses,
        int minBreakMinutes,
        int minCredits,
        int targetCredits,
        int maxCredits
    )
    {
        var selected = new List<CourseClass>(requiredClasses);
        var selectedIds = requiredClasses.Select(c => c.Id).ToHashSet();
        var currentCredits = selected.Sum(c => c.Credits);
        var warnings = new List<string>();
        var replacementIds = new HashSet<int>();

        var candidateElectives = electiveClasses
            .Where(c => !selectedIds.Contains(c.Id))
            .Select(c => new
            {
                Class = c,
                Score = ScoreCandidate(
                    c,
                    preferredElectiveIds,
                    profile,
                    flexibilityLevel,
                    preferredDaysOff,
                    avoidEarlyClasses,
                    preferOnlineClasses
                )
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Class.Id)
            .Select(x => x.Class)
            .ToList();

        foreach (var elective in candidateElectives)
        {
            var candidate = elective;

            if (selectedIds.Contains(candidate.Id))
            {
                continue;
            }

            if (preferredElectiveIds.Contains(elective.Id)
                && ClassViolatesPreferences(elective, preferredDaysOff, avoidEarlyClasses, preferOnlineClasses))
            {
                var replacement = FindReplacementForPreferredElective(
                    elective,
                    electiveClasses,
                    selected,
                    selectedIds,
                    preferredDaysOff,
                    avoidEarlyClasses,
                    preferOnlineClasses,
                    minBreakMinutes,
                    currentCredits,
                    maxCredits
                );

                if (replacement is not null)
                {
                    candidate = replacement;
                    replacementIds.Add(replacement.Id);
                    warnings.Add(
                        $"Preferred elective {elective.CourseCode} conflicted with preferences and was replaced by {replacement.CourseCode}."
                    );
                }
                else
                {
                    var alternatives = FindElectiveAlternatives(
                        elective,
                        electiveClasses,
                        preferredDaysOff,
                        avoidEarlyClasses,
                        preferOnlineClasses
                    );

                    var alternativesText = alternatives.Count > 0
                        ? $" Suggested alternatives: {string.Join(", ", alternatives.Select(c => c.CourseCode))}."
                        : " No close alternatives found for this elective.";

                    warnings.Add(
                        $"Preferred elective {elective.CourseCode} conflicts with preference(s): {string.Join(", ", DescribePreferenceViolations(elective, preferredDaysOff, avoidEarlyClasses, preferOnlineClasses))}.{alternativesText}"
                    );
                }
            }

            if (selectedIds.Contains(candidate.Id))
            {
                continue;
            }

            var nextCredits = currentCredits + candidate.Credits;
            if (nextCredits > maxCredits)
            {
                continue;
            }

            if (HasTimeConflict(selected, candidate, minBreakMinutes))
            {
                continue;
            }

            if (ClassViolatesPreferences(candidate, preferredDaysOff, avoidEarlyClasses, preferOnlineClasses) && profile.StrictnessBias >= 0.8)
            {
                continue;
            }

            selected.Add(candidate);
            selectedIds.Add(candidate.Id);
            currentCredits = nextCredits;

            if (currentCredits >= targetCredits)
            {
                break;
            }
        }

        if (currentCredits < minCredits)
        {
            foreach (var backfill in allClasses.Where(c => !selectedIds.Contains(c.Id)).OrderBy(c => c.Credits).ThenBy(c => c.Id))
            {
                var nextCredits = currentCredits + backfill.Credits;
                if (nextCredits > maxCredits)
                {
                    continue;
                }

                if (HasTimeConflict(selected, backfill, minBreakMinutes))
                {
                    continue;
                }

                selected.Add(backfill);
                selectedIds.Add(backfill.Id);
                currentCredits = nextCredits;

                if (currentCredits >= minCredits)
                {
                    break;
                }
            }
        }

        var classes = selected
            .Select(course => MapToStudentClassDto(course, requiredClasses, preferredElectiveIds, replacementIds))
            .OrderBy(c => ParsePrimaryDay(c.DaysTimes))
            .ThenBy(c => ParseStartMinutes(c.DaysTimes))
            .ToList();

        return new OptionBuildResult(
            new GeneratedScheduleDto
            {
                ScheduleId = profile.ScheduleId,
                Title = profile.Title,
                TotalCredits = classes.Sum(c => c.Credits),
                Classes = classes,
                HasConflicts = CountConflicts(selected, minBreakMinutes) > 0,
                Notes = string.Empty
            },
            warnings
        );
    }

    private CourseClass? FindReplacementForPreferredElective(
        CourseClass preferred,
        List<CourseClass> electiveClasses,
        List<CourseClass> selected,
        HashSet<int> selectedIds,
        HashSet<string> preferredDaysOff,
        bool avoidEarlyClasses,
        bool preferOnlineClasses,
        int minBreakMinutes,
        int currentCredits,
        int maxCredits
    )
    {
        return FindElectiveAlternatives(preferred, electiveClasses, preferredDaysOff, avoidEarlyClasses, preferOnlineClasses)
            .FirstOrDefault(candidate =>
                !selectedIds.Contains(candidate.Id)
                && currentCredits + candidate.Credits <= maxCredits
                && !HasTimeConflict(selected, candidate, minBreakMinutes)
            );
    }

    private static (int Min, int Target, int Max) ResolveTargetCredits(int minCredits, int maxCredits, int flexibilityLevel, int optionNumber)
    {
        var spread = maxCredits - minCredits;
        var baseWeight = flexibilityLevel / 5.0;

        var optionOffset = optionNumber switch
        {
            1 => -0.2,
            2 => 0.0,
            _ => 0.2
        };

        var targetWeight = Math.Clamp(baseWeight + optionOffset, 0.2, 1.0);
        var target = minCredits + (int)Math.Round(spread * targetWeight);
        return (minCredits, Math.Clamp(target, minCredits, maxCredits), maxCredits);
    }

    private double ScoreCandidate(
        CourseClass candidate,
        HashSet<int> preferredElectiveIds,
        OptionProfile profile,
        int flexibilityLevel,
        HashSet<string> preferredDaysOff,
        bool avoidEarlyClasses,
        bool preferOnlineClasses
    )
    {
        var score = 0.0;
        var flexibilityScale = flexibilityLevel / 5.0;

        if (preferredElectiveIds.Contains(candidate.Id))
        {
            score += 100;
        }

        if (avoidEarlyClasses)
        {
            var startMinutes = candidate.StartTime.Hour * 60 + candidate.StartTime.Minute;
            var normalized = Math.Clamp((startMinutes - 8 * 60) / 480.0, 0, 1);
            score += normalized * 30 * profile.PreferLaterStartsBias;
        }

        if (preferOnlineClasses)
        {
            score += IsOnline(candidate) ? 30 * profile.PreferOnlineBias : 0;
        }

        if (preferredDaysOff.Count > 0 && !DoesClassConflictWithDaysOff(candidate.DaysOfWeek, preferredDaysOff))
        {
            score += 20 * profile.StrictnessBias;
        }

        if (ClassViolatesPreferences(candidate, preferredDaysOff, avoidEarlyClasses, preferOnlineClasses))
        {
            score -= 35 * profile.StrictnessBias;
        }

        score += (6 - Math.Min(candidate.Credits, 5)) * 2 * (1 - flexibilityScale);
        score += _random.NextDouble() * (5 + flexibilityScale * 10);
        return score;
    }

    private List<string> BuildRequiredPreferenceWarnings(
        List<CourseClass> allClasses,
        List<CourseClass> requiredClasses,
        HashSet<string> preferredDaysOff,
        bool avoidEarlyClasses,
        bool preferOnlineClasses
    )
    {
        var warnings = new List<string>();
        foreach (var required in requiredClasses)
        {
            var violations = DescribePreferenceViolations(required, preferredDaysOff, avoidEarlyClasses, preferOnlineClasses);
            if (violations.Count > 0)
            {
                var sameCourseAlternatives = allClasses
                    .Where(c => c.Id != required.Id)
                    .Where(c => string.Equals(c.CourseCode, required.CourseCode, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var hasPreferenceMatchingAlternative = sameCourseAlternatives.Any(
                    c => !ClassViolatesPreferences(c, preferredDaysOff, avoidEarlyClasses, preferOnlineClasses)
                );

                if (!hasPreferenceMatchingAlternative)
                {
                    warnings.Add(
                        $"Required class {required.CourseCode} cannot meet your preference(s): {string.Join(", ", violations)}. It is only available at {required.StartTime:HH\\:mm}-{required.EndTime:HH\\:mm} on {string.Join("/", ParseDays(required.DaysOfWeek))}."
                    );
                }
                else
                {
                    warnings.Add(
                        $"Required class {required.CourseCode} conflicts with preference(s): {string.Join(", ", violations)}. Consider a different section/time for this required course."
                    );
                }
            }
        }
        return warnings;
    }

    private List<CourseClass> FindElectiveAlternatives(
        CourseClass preferred,
        List<CourseClass> electiveClasses,
        HashSet<string> preferredDaysOff,
        bool avoidEarlyClasses,
        bool preferOnlineClasses
    )
    {
        var departmentPrefix = GetDepartmentPrefix(preferred.CourseCode);
        return electiveClasses
            .Where(c => c.Id != preferred.Id)
            .Where(c => string.Equals(GetDepartmentPrefix(c.CourseCode), departmentPrefix, StringComparison.OrdinalIgnoreCase))
            .Where(c => c.Credits == preferred.Credits)
            .Where(c => !ClassViolatesPreferences(c, preferredDaysOff, avoidEarlyClasses, preferOnlineClasses))
            .OrderBy(c => c.StartTime)
            .ThenBy(c => c.CourseCode)
            .Take(3)
            .ToList();
    }

    private static string GetDepartmentPrefix(string courseCode)
    {
        if (string.IsNullOrWhiteSpace(courseCode))
        {
            return string.Empty;
        }

        var letters = new string(courseCode.TakeWhile(char.IsLetter).ToArray());
        return letters.Trim().ToUpperInvariant();
    }

    private static string NormalizeSemester(string semester)
    {
        if (string.IsNullOrWhiteSpace(semester))
        {
            return string.Empty;
        }

        var normalized = semester.Trim().ToUpperInvariant();
        if (normalized.StartsWith("FALL"))
        {
            return "Fall";
        }

        if (normalized.StartsWith("SPRING"))
        {
            return "Spring";
        }

        if (normalized.StartsWith("SUMMER"))
        {
            return "Summer";
        }

        return semester.Trim();
    }

    private List<string> DescribePreferenceViolations(
        CourseClass course,
        HashSet<string> preferredDaysOff,
        bool avoidEarlyClasses,
        bool preferOnlineClasses
    )
    {
        var violations = new List<string>();
        if (preferredDaysOff.Count > 0 && DoesClassConflictWithDaysOff(course.DaysOfWeek, preferredDaysOff))
        {
            violations.Add("preferred day off");
        }

        if (avoidEarlyClasses && course.StartTime < new TimeOnly(10, 0))
        {
            violations.Add("later start time preference");
        }

        if (preferOnlineClasses && !IsOnline(course))
        {
            violations.Add("online/remote preference");
        }

        return violations;
    }

    private bool ClassViolatesPreferences(
        CourseClass course,
        HashSet<string> preferredDaysOff,
        bool avoidEarlyClasses,
        bool preferOnlineClasses
    )
    {
        if (preferredDaysOff.Count > 0 && DoesClassConflictWithDaysOff(course.DaysOfWeek, preferredDaysOff))
        {
            return true;
        }

        if (avoidEarlyClasses && course.StartTime < new TimeOnly(10, 0))
        {
            return true;
        }

        if (preferOnlineClasses && !IsOnline(course))
        {
            return true;
        }

        return false;
    }

    private static bool IsOnline(CourseClass course)
    {
        var location = course.Location ?? string.Empty;
        return location.Contains("online", StringComparison.OrdinalIgnoreCase)
            || location.Contains("remote", StringComparison.OrdinalIgnoreCase)
            || location.Contains("virtual", StringComparison.OrdinalIgnoreCase)
            || location.Contains("zoom", StringComparison.OrdinalIgnoreCase);
    }

    private bool DoesClassConflictWithDaysOff(string daysOfWeek, HashSet<string> daysToAvoid)
    {
        if (string.IsNullOrEmpty(daysOfWeek))
            return false;

        var classDays = daysOfWeek.Split(',')
            .Select(d => d.Trim())
            .ToHashSet();

        return classDays.Any(d => daysToAvoid.Contains(d));
    }

    private bool HasTimeConflict(List<CourseClass> selected, CourseClass candidate, int minBreakMinutes)
    {
        foreach (var existing in selected)
        {
            if (ClassesOverlap(existing, candidate, minBreakMinutes))
                return true;
        }
        return false;
    }

    private bool ClassesOverlap(CourseClass class1, CourseClass class2, int minBreakMinutes)
    {
        var days1 = ParseDays(class1.DaysOfWeek);
        var days2 = ParseDays(class2.DaysOfWeek);

        var sharedDays = days1.Intersect(days2).Any();
        if (!sharedDays)
            return false;

        var start1 = class1.StartTime.Hour * 60 + class1.StartTime.Minute;
        var end1 = class1.EndTime.Hour * 60 + class1.EndTime.Minute;
        var start2 = class2.StartTime.Hour * 60 + class2.StartTime.Minute;
        var end2 = class2.EndTime.Hour * 60 + class2.EndTime.Minute;

        var overlap = start1 < end2 && start2 < end1;
        if (overlap)
        {
            return true;
        }

        if (minBreakMinutes <= 0)
        {
            return false;
        }

        var gap = Math.Max(start1, start2) - Math.Min(end1, end2);
        return gap >= 0 && gap < minBreakMinutes;
    }

    private HashSet<string> ParseDays(string daysString)
    {
        if (string.IsNullOrEmpty(daysString))
            return ["Mon"];

        var normalized = daysString.Trim().ToUpperInvariant();
        var days = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (normalized.Contains("MON") || normalized.Contains('M'))
        {
            days.Add("Mon");
        }

        if (normalized.Contains("TUE") || normalized.Contains("TU") || normalized.Contains('T'))
        {
            days.Add("Tue");
        }

        if (normalized.Contains("WED") || normalized.Contains('W'))
        {
            days.Add("Wed");
        }

        if (normalized.Contains("THU") || normalized.Contains('R') || normalized.Contains('H'))
        {
            days.Add("Thu");
        }

        if (normalized.Contains("FRI") || normalized.Contains('F'))
        {
            days.Add("Fri");
        }

        if (days.Count > 0)
        {
            return days;
        }

        return new HashSet<string>(
            daysString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .Where(d => !string.IsNullOrEmpty(d)),
            StringComparer.OrdinalIgnoreCase
        );
    }

    private int CountConflicts(List<CourseClass> classes, int minBreakMinutes)
    {
        var count = 0;
        for (var i = 0; i < classes.Count; i++)
        {
            for (var j = i + 1; j < classes.Count; j++)
            {
                if (ClassesOverlap(classes[i], classes[j], minBreakMinutes))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int ParsePrimaryDay(string daysTimes)
    {
        if (string.IsNullOrWhiteSpace(daysTimes))
        {
            return int.MaxValue;
        }

        for (var index = 0; index < PreferredDayOrder.Length; index++)
        {
            if (daysTimes.Contains(PreferredDayOrder[index], StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    private static int ParseStartMinutes(string daysTimes)
    {
        if (string.IsNullOrWhiteSpace(daysTimes))
        {
            return int.MaxValue;
        }

        var parts = daysTimes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var timePart = parts.FirstOrDefault(p => p.Contains('-'));
        if (timePart is null)
        {
            return int.MaxValue;
        }

        var startToken = timePart.Split('-', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (startToken is null)
        {
            return int.MaxValue;
        }

        if (!TimeOnly.TryParse(startToken, out var start))
        {
            return int.MaxValue;
        }

        return start.Hour * 60 + start.Minute;
    }

    private StudentClassDto MapToStudentClassDto(
        CourseClass course,
        List<CourseClass> requiredClasses,
        HashSet<int> preferredElectiveIds,
        HashSet<int> replacementIds
    )
    {
        var role = "general";
        if (requiredClasses.Any(c => c.Id == course.Id))
        {
            role = "required";
        }
        else if (replacementIds.Contains(course.Id))
        {
            role = "replacement";
        }
        else if (preferredElectiveIds.Contains(course.Id))
        {
            role = "preferred-elective";
        }

        return new StudentClassDto
        {
            ClassId = course.Id,
            CourseCode = course.CourseCode,
            ClassName = course.ClassName,
            InstructorName = course.Instructor != null 
                ? $"{course.Instructor.FirstName} {course.Instructor.LastName}" 
                : "TBD",
            DaysTimes = $"{string.Join("/", ParseDays(course.DaysOfWeek))} {course.StartTime.ToString("HH:mm")}-{course.EndTime.ToString("HH:mm")}",
            Days = ParseDays(course.DaysOfWeek).ToList(),
            StartTime = course.StartTime.ToString("HH:mm"),
            EndTime = course.EndTime.ToString("HH:mm"),
            Term = course.Semester,
            Location = course.Location,
            Credits = course.Credits,
            Role = role,
            IsWaitlisted = false
        };
    }

    private sealed record OptionProfile(int ScheduleId, string Title, double StrictnessBias, double PreferLaterStartsBias, double PreferOnlineBias);

    private sealed record OptionBuildResult(GeneratedScheduleDto Schedule, List<string> Warnings);
}