using Chronos.Domain.Resources;
using Chronos.Domain.Schedule;
using Chronos.Tests.Engine.TestFixtures;

namespace Chronos.Tests.Engine.Specification;

/// <summary>
/// Verifies post-condition invariants I1–I8 for specification tests.
/// </summary>
public static class SchedulingInvariantVerifier
{
    public static IReadOnlyList<string> Verify(
        IReadOnlyList<Assignment> assignments,
        IReadOnlyList<Activity> activities,
        IReadOnlyDictionary<Guid, Slot> slotsById,
        DateTime periodFrom,
        DateTime periodTo,
        IReadOnlyList<Guid>? unscheduledActivityIds = null
    )
    {
        var violations = new List<string>();
        var activitiesById = activities.ToDictionary(a => a.Id);
        var requiredWeeks = SchedulingWeekHelper.GetIsoWeekNumbers(periodFrom, periodTo);
        var unscheduled = unscheduledActivityIds?.ToHashSet() ?? [];

        VerifySlotRoomUniqueness(assignments, violations);
        VerifyTeacherUniqueness(assignments, activitiesById, slotsById, violations);

        foreach (var activity in activities)
        {
            foreach (var weekNum in requiredWeeks)
            {
                var rows = assignments
                    .Where(a => a.ActivityId == activity.Id && a.WeekNum == weekNum)
                    .ToList();

                if (rows.Count == 0)
                    continue;

                VerifyStreakForActivityWeek(activity, weekNum, rows, slotsById, violations);
            }
        }

        if (unscheduled.Count > 0)
            VerifyUnscheduledReporting(assignments, activities, requiredWeeks, unscheduled, violations);

        return violations;
    }

    public static IReadOnlyList<string> VerifyPerWeekCompleteness(
        IReadOnlyList<Assignment> assignments,
        Activity activity,
        DateTime periodFrom,
        DateTime periodTo
    )
    {
        var violations = new List<string>();
        var requiredWeeks = SchedulingWeekHelper.GetIsoWeekNumbers(periodFrom, periodTo);
        var weeksWithRows = assignments
            .Where(a => a.ActivityId == activity.Id && a.WeekNum.HasValue)
            .Select(a => a.WeekNum!.Value)
            .Distinct()
            .ToHashSet();

        if (weeksWithRows.Count == 0)
            return violations;

        foreach (var week in requiredWeeks)
        {
            if (!weeksWithRows.Contains(week))
            {
                violations.Add(
                    $"I2: Activity {activity.Id} has assignments in weeks [{string.Join(", ", weeksWithRows.OrderBy(w => w))}] but is missing required week {week}"
                );
            }
        }

        return violations;
    }

    private static void VerifyUnscheduledReporting(
        IReadOnlyList<Assignment> assignments,
        IReadOnlyList<Activity> activities,
        List<int> requiredWeeks,
        HashSet<Guid> unscheduled,
        List<string> violations
    )
    {
        foreach (var activityId in unscheduled)
        {
            var rows = assignments.Where(a => a.ActivityId == activityId).ToList();
            if (rows.Count == 0)
                continue;

            violations.Add(
                $"I8/reporting: Activity {activityId} is in UnscheduledActivityIds but has {rows.Count} assignment row(s) in weeks [{string.Join(", ", rows.Where(r => r.WeekNum.HasValue).Select(r => r.WeekNum!.Value).Distinct().OrderBy(w => w))}]"
            );
        }

        foreach (var activity in activities)
        {
            if (unscheduled.Contains(activity.Id))
                continue;

            var weeksPresent = assignments
                .Where(a => a.ActivityId == activity.Id && a.WeekNum.HasValue)
                .Select(a => a.WeekNum!.Value)
                .Distinct()
                .ToHashSet();

            if (weeksPresent.Count == 0)
                continue;

            if (weeksPresent.Count < requiredWeeks.Count)
            {
                var missing = requiredWeeks.Where(w => !weeksPresent.Contains(w)).ToList();
                violations.Add(
                    $"I2/reporting: Activity {activity.Id} is not unscheduled but missing weeks [{string.Join(", ", missing)}] (present: [{string.Join(", ", weeksPresent.OrderBy(w => w))}])"
                );
            }
        }
    }

    private static void VerifySlotRoomUniqueness(
        IReadOnlyList<Assignment> assignments,
        List<string> violations
    )
    {
        var groups = assignments
            .Where(a => a.WeekNum.HasValue)
            .GroupBy(a => (a.WeekNum!.Value, a.SlotId, a.ResourceId))
            .Where(g => g.Count() > 1);

        foreach (var g in groups)
        {
            violations.Add(
                $"I3: Duplicate (WeekNum={g.Key.Item1}, SlotId={g.Key.Item2}, ResourceId={g.Key.Item3}) — {g.Count()} rows"
            );
        }
    }

    private static void VerifyTeacherUniqueness(
        IReadOnlyList<Assignment> assignments,
        Dictionary<Guid, Activity> activitiesById,
        IReadOnlyDictionary<Guid, Slot> slotsById,
        List<string> violations
    )
    {
        var byWeekSlot = assignments
            .Where(a => a.WeekNum.HasValue)
            .GroupBy(a => (a.WeekNum!.Value, a.SlotId));

        foreach (var group in byWeekSlot)
        {
            var teachers = group
                .Select(a => activitiesById.TryGetValue(a.ActivityId, out var act) ? act.AssignedUserId : Guid.Empty)
                .Where(t => t != Guid.Empty)
                .ToList();

            if (teachers.Count <= 1)
                continue;

            if (teachers.Distinct().Count() < teachers.Count)
            {
                violations.Add(
                    $"I4: Teacher double-booked at WeekNum={group.Key.Item1}, SlotId={group.Key.Item2} ({teachers.Count} assignments)"
                );
            }
        }
    }

    private static void VerifyStreakForActivityWeek(
        Activity activity,
        int weekNum,
        List<Assignment> rows,
        IReadOnlyDictionary<Guid, Slot> slotsById,
        List<string> violations
    )
    {
        var resourceIds = rows.Select(r => r.ResourceId).Distinct().ToList();
        if (resourceIds.Count != 1)
        {
            violations.Add(
                $"I6: Activity {activity.Id} week {weekNum} uses multiple resources: [{string.Join(", ", resourceIds)}]"
            );
            return;
        }

        var streakSlots = rows
            .Select(r => slotsById.TryGetValue(r.SlotId, out var s) ? s : null)
            .Where(s => s != null)
            .Cast<Slot>()
            .OrderBy(s => s.FromTime)
            .ToList();

        if (streakSlots.Count != rows.Count)
        {
            violations.Add($"I6: Activity {activity.Id} week {weekNum} references unknown slot ids");
            return;
        }

        var weekdays = streakSlots.Select(s => s.Weekday).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (weekdays.Count != 1)
        {
            violations.Add(
                $"I6: Activity {activity.Id} week {weekNum} spans weekdays [{string.Join(", ", weekdays)}]"
            );
        }

        for (var i = 1; i < streakSlots.Count; i++)
        {
            if (streakSlots[i - 1].ToTime != streakSlots[i].FromTime)
            {
                violations.Add(
                    $"I6: Activity {activity.Id} week {weekNum} slots not consecutive at index {i}"
                );
            }
        }

        var totalMinutes = streakSlots.Sum(s => (int)(s.ToTime - s.FromTime).TotalMinutes);
        if (activity.Duration > 0 && totalMinutes != activity.Duration)
        {
            violations.Add(
                $"I5: Activity {activity.Id} week {weekNum} total slot duration {totalMinutes} min != required {activity.Duration} min"
            );
        }

        if (activity.Duration > 0 && streakSlots.Count > 0)
        {
            var slotMinutes = (int)(streakSlots[0].ToTime - streakSlots[0].FromTime).TotalMinutes;
            if (slotMinutes > 0)
            {
                var expectedRows = (int)Math.Ceiling(activity.Duration / (double)slotMinutes);
                if (streakSlots.All(s => (int)(s.ToTime - s.FromTime).TotalMinutes == slotMinutes)
                    && rows.Count != expectedRows
                    && totalMinutes == activity.Duration)
                {
                    violations.Add(
                        $"I1/I2: Activity {activity.Id} week {weekNum} has {rows.Count} rows, expected {expectedRows} for uniform {slotMinutes}-min slots"
                    );
                }
            }
        }
    }
}
