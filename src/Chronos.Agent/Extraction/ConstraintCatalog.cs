namespace Chronos.Agent.Extraction;

public static class ConstraintCatalog
{
    public static readonly HashSet<string> HardConstraintKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "avoid_weekday"
    };

    public static readonly HashSet<string> SoftPreferenceKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "preferred_weekday",
        "preferred_weekdays",
        "avoid_weekday",
        "preferred_time_morning",
        "preferred_time_afternoon",
        "preferred_time_evening",
        "preferred_timerange"
    };

    public static bool IsKnownHardConstraint(string key) => HardConstraintKeys.Contains(key);
    public static bool IsKnownSoftPreference(string key) => SoftPreferenceKeys.Contains(key);
}
