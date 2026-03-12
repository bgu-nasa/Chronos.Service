using System.Text.RegularExpressions;
using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Schedule;

namespace Chronos.Engine.Matching;

/// <summary>
/// Calculates preference weights for (Slot, Resource) pairs and performs weighted random selection
/// </summary>
public class PreferenceWeightedRanker(
    IUserPreferenceRepository userPreferenceRepository,
    ILogger<PreferenceWeightedRanker> logger
)
{
    private readonly IUserPreferenceRepository _userPreferenceRepository = userPreferenceRepository;
    private readonly ILogger<PreferenceWeightedRanker> _logger = logger;
    private readonly Random _random = new();

    /// <summary>
    /// Calculate preference weight for a (Slot, Resource) pair based on user preferences
    /// </summary>
    public async Task<double> CalculateWeightAsync(
        SlotResourcePair candidate,
        Guid userId,
        Guid organizationId,
        Guid schedulingPeriodId
    )
    {
        _logger.LogTrace(
            "Calculating weight for candidate (Slot: {SlotId}, Resource: {ResourceId}) for User {UserId}",
            candidate.SlotId,
            candidate.ResourceId,
            userId
        );

        var preferences = await _userPreferenceRepository.GetByUserPeriodAsync(
            userId,
            schedulingPeriodId
        );

        _logger.LogTrace(
            "Loaded {PreferenceCount} preferences for User {UserId}",
            preferences.Count,
            userId
        );

        if (!preferences.Any())
        {
            return 1.0; // Neutral weight if no preferences
        }

        double weight = 1.0;
        var matchedPreferences = 0;

        foreach (var pref in preferences)
        {
            if (CandidateMatchesPreference(candidate, pref))
            {
                matchedPreferences++;
                var multiplier = GetPreferenceMultiplier(pref.Key, pref.Value);
                weight *= multiplier;

                _logger.LogTrace(
                    "Applied preference {PreferenceKey}={PreferenceValue} with multiplier {Multiplier} to candidate {Candidate}",
                    pref.Key,
                    pref.Value,
                    multiplier,
                    candidate
                );
            }
        }

        _logger.LogTrace(
            "Final weight for candidate: {Weight:F2} (matched {MatchedCount}/{TotalCount} preferences)",
            weight,
            matchedPreferences,
            preferences.Count
        );

        return weight;
    }

    /// <summary>
    /// Select one candidate using weighted random sampling
    /// Higher weight = higher probability of selection
    /// </summary>
    public SlotResourcePair SelectRandomWeighted(
        List<SlotResourcePair> candidates,
        double[] weights
    )
    {
        if (candidates.Count == 0)
        {
            throw new ArgumentException(
                "Cannot select from empty candidate list",
                nameof(candidates)
            );
        }

        if (candidates.Count != weights.Length)
        {
            throw new ArgumentException("Candidates and weights must have same length");
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        // Calculate cumulative weights
        double totalWeight = weights.Sum();

        if (totalWeight <= 0)
        {
            // All weights are zero or negative, fall back to uniform random
            _logger.LogWarning("All weights are non-positive, using uniform random selection");
            return candidates[_random.Next(candidates.Count)];
        }

        // Generate random value in [0, totalWeight)
        double randomValue = _random.NextDouble() * totalWeight;

        // Find the candidate corresponding to this random value
        double cumulative = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += weights[i];
            if (randomValue < cumulative)
            {
                _logger.LogDebug(
                    "Selected candidate {Index} with weight {Weight} (cumulative: {Cumulative}, random: {Random})",
                    i,
                    weights[i],
                    cumulative,
                    randomValue
                );

                return candidates[i];
            }
        }

        // Fallback (should rarely happen due to floating point precision)
        return candidates[^1];
    }

    private bool CandidateMatchesPreference(SlotResourcePair candidate, UserPreference preference)
    {
        // Example matching logic - can be extended based on preference types
        return preference.Key switch
        {
            "preferred_weekday" => MatchesPreferredWeekday(candidate, preference.Value),
            "preferred_weekdays" => MatchesPreferredWeekdays(candidate, preference.Value),
            "avoid_weekday" => candidate.Slot.Weekday.Equals(
                preference.Value,
                StringComparison.OrdinalIgnoreCase
            ),
            "preferred_time_morning" => candidate.Slot.FromTime.Hours < 12,
            "preferred_time_afternoon" => candidate.Slot.FromTime.Hours >= 12
                && candidate.Slot.FromTime.Hours < 17,
            "preferred_time_evening" => candidate.Slot.FromTime.Hours >= 17,
            "preferred_timerange" => MatchesPreferredTimeRange(candidate, preference.Value),
            _ => false,
        };
    }

    private bool MatchesPreferredWeekday(SlotResourcePair candidate, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // Normalize both weekdays for comparison
        var slotWeekday = NormalizeWeekday(candidate.Slot.Weekday);
        var preferredWeekday = NormalizeWeekday(value.Trim());

        return string.Equals(slotWeekday, preferredWeekday, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesPreferredWeekdays(SlotResourcePair candidate, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // Parse comma-separated weekdays
        var preferredWeekdays = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => NormalizeWeekday(w.Trim()))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!preferredWeekdays.Any())
        {
            return false;
        }

        // Normalize slot weekday and check if it's in the preferred list
        var slotWeekday = NormalizeWeekday(candidate.Slot.Weekday);
        return preferredWeekdays.Contains(slotWeekday, StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeWeekday(string weekday)
    {
        if (string.IsNullOrWhiteSpace(weekday))
        {
            return weekday;
        }

        // Normalize to capitalized format: "Monday", "Tuesday", etc.
        var normalized = weekday.Trim();
        
        // Capitalize first letter, lowercase the rest
        if (normalized.Length > 0)
        {
            normalized = char.ToUpperInvariant(normalized[0]) + 
                        (normalized.Length > 1 ? normalized.Substring(1).ToLowerInvariant() : "");
        }

        return normalized;
    }

    private bool MatchesPreferredTimeRange(SlotResourcePair candidate, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // Parse multiple preferred time ranges (comma or newline separated)
        var preferredRanges = ParsePreferredTimeRanges(value);

        if (!preferredRanges.Any())
        {
            return false;
        }

        // Check if slot falls within any preferred time range
        foreach (var preferredRange in preferredRanges)
        {
            // Normalize both weekdays for comparison
            var slotWeekday = NormalizeWeekday(candidate.Slot.Weekday);
            var preferredWeekday = NormalizeWeekday(preferredRange.Weekday);

            // Check if weekday matches (case-insensitive)
            if (!string.Equals(slotWeekday, preferredWeekday, StringComparison.OrdinalIgnoreCase))
            {
                continue; // Different weekday, check next range
            }

            // Check if slot falls entirely within the preferred time range
            // Slot is within preferred range if: slotStart >= preferredStart AND slotEnd <= preferredEnd
            if (candidate.Slot.FromTime >= preferredRange.StartTime && candidate.Slot.ToTime <= preferredRange.EndTime)
            {
                return true; // Slot matches a preferred range
            }
        }

        return false; // No preferred range matches
    }

    private List<PreferredTimeRange> ParsePreferredTimeRanges(string value)
    {
        var ranges = new List<PreferredTimeRange>();

        // Split by comma or newline
        var entries = value.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Pattern: "Weekday HH:mm - HH:mm" or "Weekday HH:mm-HH:mm"
        // Examples: "Monday 09:30 - 11:00", "Tuesday 13:00-15:00"
        var pattern = @"^(\w+)\s+(\d{1,2}:\d{2})\s*-\s*(\d{1,2}:\d{2})$";

        foreach (var entry in entries)
        {
            var trimmedEntry = entry.Trim();
            if (string.IsNullOrWhiteSpace(trimmedEntry))
            {
                continue;
            }

            var match = Regex.Match(trimmedEntry, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                _logger.LogWarning(
                    "Invalid preferred_timerange format: '{Entry}'. Expected format: 'Weekday HH:mm - HH:mm'",
                    trimmedEntry
                );
                continue;
            }

            var weekdayRaw = match.Groups[1].Value;
            // Normalize weekday to match database format (capitalized: "Monday", "Tuesday", etc.)
            var weekday = NormalizeWeekday(weekdayRaw);
            var startTimeStr = match.Groups[2].Value;
            var endTimeStr = match.Groups[3].Value;

            if (!TimeSpan.TryParse(startTimeStr, out var startTime) ||
                !TimeSpan.TryParse(endTimeStr, out var endTime))
            {
                _logger.LogWarning(
                    "Invalid time format in preferred_timerange: '{Entry}'. Use HH:mm format",
                    trimmedEntry
                );
                continue;
            }

            if (startTime >= endTime)
            {
                _logger.LogWarning(
                    "Start time must be before end time in preferred_timerange: '{Entry}'",
                    trimmedEntry
                );
                continue;
            }

            ranges.Add(new PreferredTimeRange
            {
                Weekday = weekday,
                StartTime = startTime,
                EndTime = endTime
            });
        }

        return ranges;
    }

    private class PreferredTimeRange
    {
        public string Weekday { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }

    private double GetPreferenceMultiplier(string key, string value)
    {
        // Weight multipliers based on preference type
        return key switch
        {
            "preferred_weekday" => 3.0,
            "preferred_weekdays" => 3.0,
            "preferred_time_morning" => 3.0,
            "preferred_time_afternoon" => 2.0,
            "preferred_time_evening" => 2.0,
            "preferred_timerange" => 4.0,
            "avoid_weekday" => 0.3,
            "avoid_time_morning" => 0.3,
            "avoid_time_afternoon" => 0.5,
            "avoid_time_evening" => 0.5,
            _ => 1.0, // Neutral for unknown preferences
        };
    }
}
