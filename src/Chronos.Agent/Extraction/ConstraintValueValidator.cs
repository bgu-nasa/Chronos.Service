using System.Text.RegularExpressions;

namespace Chronos.Agent.Extraction;

/// <summary>
/// Validates the <c>Value</c> portion of an extracted constraint or preference
/// against the format expected for its <c>Key</c>. The Ollama schema only restricts
/// the key to the known catalog — the value is still free-form text — so this
/// catches things like "Funday" or "tomorrow" before they reach the user.
/// </summary>
public static class ConstraintValueValidator
{
    private static readonly HashSet<string> Weekdays = new(StringComparer.OrdinalIgnoreCase)
    {
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"
    };

    private static readonly HashSet<string> Booleans = new(StringComparer.OrdinalIgnoreCase)
    {
        "true", "false"
    };

    // "Day HH:mm - HH:mm" — capturing weekday so it can be validated separately.
    private static readonly Regex TimeRangeEntry = new(
        @"^\s*(?<day>[A-Za-z]+)\s+(?<from>\d{1,2}:\d{2})\s*-\s*(?<to>\d{1,2}:\d{2})\s*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Validates <paramref name="value"/> against the format rules for <paramref name="key"/>.
    /// Returns <c>null</c> when valid, or a human-readable error message describing the problem.
    /// Unknown keys are rejected — callers should validate the key separately first.
    /// </summary>
    public static string? Validate(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Value is empty.";

        return key switch
        {
            "unavailable_day" or "avoid_weekday" or "preferred_weekday" => ValidateWeekday(value),
            "preferred_weekdays" => ValidateWeekdayList(value),
            "preferred_time_morning" or "preferred_time_afternoon" or "preferred_time_evening"
                => ValidateBoolean(value),
            "preferred_timerange" => ValidateTimeRangeList(value),
            _ => $"Unknown key '{key}'."
        };
    }

    private static string? ValidateWeekday(string value)
    {
        var trimmed = value.Trim();
        return Weekdays.Contains(trimmed)
            ? null
            : $"'{value}' is not a valid weekday (expected Monday–Sunday).";
    }

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

    private static string? ValidateBoolean(string value)
    {
        return Booleans.Contains(value.Trim())
            ? null
            : $"'{value}' is not a valid boolean (expected 'true' or 'false').";
    }

    private static string? ValidateTimeRangeList(string value)
    {
        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return "Expected one or more 'Day HH:mm - HH:mm' entries.";

        foreach (var part in parts)
        {
            var match = TimeRangeEntry.Match(part);
            if (!match.Success)
                return $"'{part}' is not a valid time range (expected 'Day HH:mm - HH:mm').";

            if (!Weekdays.Contains(match.Groups["day"].Value))
                return $"'{match.Groups["day"].Value}' in '{part}' is not a valid weekday.";

            if (!IsValidTime(match.Groups["from"].Value) || !IsValidTime(match.Groups["to"].Value))
                return $"'{part}' contains an invalid time (expected HH:mm with HH 0–23, mm 0–59).";
        }

        return null;
    }

    private static bool IsValidTime(string hhmm)
    {
        var parts = hhmm.Split(':');
        if (parts.Length != 2) return false;
        return int.TryParse(parts[0], out var h) && h is >= 0 and <= 23
            && int.TryParse(parts[1], out var m) && m is >= 0 and <= 59;
    }
}
