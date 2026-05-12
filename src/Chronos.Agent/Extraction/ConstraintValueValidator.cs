using System.Text.RegularExpressions;

namespace Chronos.Agent.Extraction;

/// <summary>
/// Validates the <c>Value</c> portion of an extracted constraint or preference against
/// the format the engine actually parses. Each branch mirrors the parser of the
/// corresponding consumer in the engine:
///
/// - <c>forbidden_timerange</c> → <c>ActivityConstraintProcessor.ParseForbiddenRanges</c>
///   (the only key the user-constraint flow handles today).
/// - All soft keys → <c>PreferenceWeightedRanker.CandidateMatchesPreference</c>.
///
/// Anything that passes here will be correctly understood downstream.
/// </summary>
public static class ConstraintValueValidator
{
    private static readonly HashSet<string> Weekdays = new(StringComparer.OrdinalIgnoreCase)
    {
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"
    };

    // "Weekday HH:mm - HH:mm" — same shape ForbiddenTimeRangeValidator and
    // ActivityConstraintProcessor.ParseForbiddenRanges parse.
    private static readonly Regex TimeRangeEntry = new(
        @"^\s*(?<day>[A-Za-z]+)\s+(?<from>\d{1,2}:\d{2})\s*-\s*(?<to>\d{1,2}:\d{2})\s*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Validates <paramref name="value"/> against the format rules for <paramref name="key"/>.
    /// Returns <c>null</c> when valid, or a human-readable error message describing the problem.
    /// Keys outside <see cref="KnownConstraintKeys"/> are rejected.
    /// </summary>
    public static string? Validate(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Value is empty.";

        return key switch
        {
            // Hard (UserConstraint) — only key the engine handles for user constraints today.
            "forbidden_timerange" => ValidateTimeRangeList(value),

            // Soft (UserPreference) — all 7 PreferenceWeightedRanker branches.
            "preferred_weekday" or "avoid_weekday" => ValidateWeekday(value),
            "preferred_weekdays" => ValidateWeekdayList(value),
            "preferred_time_morning" or "preferred_time_afternoon" or "preferred_time_evening"
                => ValidateBoolean(value),
            "preferred_timerange" => ValidateTimeRangeList(value),

            _ => $"Unknown key '{key}'."
        };
    }

    /// <summary>
    /// Validates an ISO week number for a UserConstraint. Returns null when valid,
    /// or an error message. Null input is treated as valid (recurring constraint).
    /// </summary>
    public static string? ValidateWeekNum(int? weekNum)
    {
        if (weekNum is null)
            return null;
        if (weekNum is < 1 or > 53)
            return $"weekNum {weekNum} is out of ISO week range (expected 1..53).";
        return null;
    }

    // --- Single weekday (preferred_weekday, avoid_weekday) ---
    private static string? ValidateWeekday(string value)
    {
        return Weekdays.Contains(value.Trim())
            ? null
            : $"'{value}' is not a valid weekday (expected Monday–Sunday).";
    }

    // --- Comma-separated weekdays (preferred_weekdays) ---
    private static string? ValidateWeekdayList(string value)
    {
        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return "Expected a comma-separated list of weekdays.";

        var bad = parts.Where(p => !Weekdays.Contains(p)).ToList();
        return bad.Count == 0
            ? null
            : $"Invalid weekday(s): {string.Join(", ", bad)} (expected Monday–Sunday).";
    }

    // --- Boolean (preferred_time_morning/afternoon/evening) ---
    private static string? ValidateBoolean(string value)
    {
        var t = value.Trim();
        return string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
            ? null
            : $"'{value}' is not a valid boolean (expected 'true' or 'false').";
    }

    // --- Comma- or newline-separated "Weekday HH:mm - HH:mm" entries
    //     (forbidden_timerange, preferred_timerange) ---
    private static string? ValidateTimeRangeList(string value)
    {
        var parts = value
            .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return "Expected one or more 'Weekday HH:mm - HH:mm' entries.";

        foreach (var part in parts)
        {
            var match = TimeRangeEntry.Match(part);
            if (!match.Success)
                return $"'{part}' is not a valid time range (expected 'Weekday HH:mm - HH:mm').";

            if (!Weekdays.Contains(match.Groups["day"].Value))
                return $"'{match.Groups["day"].Value}' in '{part}' is not a valid weekday.";

            if (!TryParseHHmm(match.Groups["from"].Value, out var from) ||
                !TryParseHHmm(match.Groups["to"].Value, out var to))
                return $"'{part}' contains an invalid time (expected HH:mm with HH 0–23, mm 0–59).";

            if (from >= to)
                return $"'{part}': start time must be before end time.";
        }

        return null;
    }

    private static bool TryParseHHmm(string hhmm, out TimeSpan ts)
    {
        ts = default;
        var parts = hhmm.Split(':');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], out var h) || h is < 0 or > 23) return false;
        if (!int.TryParse(parts[1], out var m) || m is < 0 or > 59) return false;
        ts = new TimeSpan(h, m, 0);
        return true;
    }
}
