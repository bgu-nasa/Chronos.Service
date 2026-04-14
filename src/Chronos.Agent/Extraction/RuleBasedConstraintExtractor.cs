using Chronos.Agent.Conversation;

namespace Chronos.Agent.Extraction;

public sealed class RuleBasedConstraintExtractor : IConstraintExtractor
{
    private static readonly string[] OrderedWeekdays =
    [
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"
    ];

    public Task<ExtractedConstraintSet> ExtractAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var latestUserMessage = messages
            .LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))?.Content
            ?? string.Empty;

        var lower = latestUserMessage.ToLowerInvariant();
        var set = new ExtractedConstraintSet();

        var mentionedDays = OrderedWeekdays.Where(d => lower.Contains(d.ToLowerInvariant())).ToList();

        var hasUnavailabilityIntent =
            lower.Contains("can't work") ||
            lower.Contains("cannot work") ||
            lower.Contains("avoid") ||
            lower.Contains("unavailable") ||
            lower.Contains("not available");

        if (hasUnavailabilityIntent)
        {
            foreach (var day in mentionedDays)
            {
                set.HardConstraints.Add(("avoid_weekday", day));
            }
        }

        if (lower.Contains("prefer") || lower.Contains("preferred"))
        {
            if (mentionedDays.Count == 1)
            {
                set.SoftPreferences.Add(("preferred_weekday", mentionedDays[0]));
            }
            else if (mentionedDays.Count > 1)
            {
                set.SoftPreferences.Add(("preferred_weekdays", string.Join(",", mentionedDays)));
            }

            if (lower.Contains("morning"))
            {
                set.SoftPreferences.Add(("preferred_time_morning", "true"));
            }

            if (lower.Contains("afternoon"))
            {
                set.SoftPreferences.Add(("preferred_time_afternoon", "true"));
            }

            if (lower.Contains("evening"))
            {
                set.SoftPreferences.Add(("preferred_time_evening", "true"));
            }
        }

        return Task.FromResult(set);
    }
}
