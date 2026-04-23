namespace Chronos.Agent.Extraction;

/// <summary>
/// Catalog of known constraint and preference keys from the Chronos domain.
/// Used for validation and prompt generation.
/// </summary>
public static class KnownConstraintKeys
{
    public static readonly IReadOnlySet<string> HardConstraintKeys = new HashSet<string>
    {
        "unavailable_day",
        "avoid_weekday"
    };

    public static readonly IReadOnlySet<string> SoftPreferenceKeys = new HashSet<string>
    {
        "preferred_weekday",
        "preferred_weekdays",
        "preferred_time_morning",
        "preferred_time_afternoon",
        "preferred_time_evening",
        "preferred_timerange"
    };

    public static readonly IReadOnlySet<string> AllKeys =
        new HashSet<string>(HardConstraintKeys.Concat(SoftPreferenceKeys));

    public static bool IsValid(string key) => AllKeys.Contains(key);
}
