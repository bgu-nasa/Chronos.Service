namespace Chronos.Agent.Extraction;

public sealed class ConstraintValidator
{
    private static readonly HashSet<string> Weekdays = new(StringComparer.OrdinalIgnoreCase)
    {
        "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"
    };

    public ConstraintValidationResult Validate(ExtractedConstraintSet set)
    {
        var result = new ConstraintValidationResult();

        foreach (var (key, value) in set.HardConstraints)
        {
            if (!ConstraintCatalog.IsKnownHardConstraint(key))
            {
                result.Errors.Add($"Unknown hard-constraint key: {key}");
                continue;
            }

            ValidateValue(key, value, result);
        }

        foreach (var (key, value) in set.SoftPreferences)
        {
            if (!ConstraintCatalog.IsKnownSoftPreference(key))
            {
                result.Errors.Add($"Unknown soft-preference key: {key}");
                continue;
            }

            ValidateValue(key, value, result);
        }

        ValidateContradictions(set, result);
        return result;
    }

    private static void ValidateValue(string key, string value, ConstraintValidationResult result)
    {
        if (string.Equals(key, "avoid_weekday", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "preferred_weekday", StringComparison.OrdinalIgnoreCase))
        {
            if (!Weekdays.Contains(value.Trim()))
            {
                result.Errors.Add($"Invalid weekday value for {key}: {value}");
            }

            return;
        }

        if (string.Equals(key, "preferred_weekdays", StringComparison.OrdinalIgnoreCase))
        {
            var days = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (days.Length == 0 || days.Any(d => !Weekdays.Contains(d)))
            {
                result.Errors.Add($"Invalid weekday list for {key}: {value}");
            }

            return;
        }

        if (key.StartsWith("preferred_time_", StringComparison.OrdinalIgnoreCase)
            && !bool.TryParse(value, out _))
        {
            result.Errors.Add($"Invalid boolean value for {key}: {value}");
        }
    }

    private static void ValidateContradictions(ExtractedConstraintSet set, ConstraintValidationResult result)
    {
        var avoidedDays = set.HardConstraints
            .Where(c => string.Equals(c.Key, "avoid_weekday", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var preferredDays = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pref in set.SoftPreferences)
        {
            if (string.Equals(pref.Key, "preferred_weekday", StringComparison.OrdinalIgnoreCase))
            {
                preferredDays.Add(pref.Value.Trim());
            }

            if (string.Equals(pref.Key, "preferred_weekdays", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var day in pref.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    preferredDays.Add(day);
                }
            }
        }

        var overlap = preferredDays.Where(avoidedDays.Contains).ToList();
        if (overlap.Count > 0)
        {
            result.Errors.Add($"Contradictory weekdays detected: {string.Join(", ", overlap)} are both preferred and avoided.");
        }
    }
}
