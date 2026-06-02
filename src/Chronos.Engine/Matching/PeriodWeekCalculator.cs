using System.Globalization;

namespace Chronos.Engine.Matching;

/// <summary>
/// Period-relative week indices (1..N) aligned with the calendar's Sunday-start week view.
/// Stored in <see cref="Domain.Schedule.Assignment.WeekNum"/> during batch scheduling.
/// </summary>
public static class PeriodWeekCalculator
{
    public static DateTime GetSundayWeekStart(DateTime date)
    {
        var d = date.Date;
        return d.AddDays(-(int)d.DayOfWeek);
    }

    public static IReadOnlyList<int> GetPeriodWeekIndices(DateTime periodFrom, DateTime periodTo)
    {
        var weeks = new List<int>();
        var weekStart = GetSundayWeekStart(periodFrom);
        var end = periodTo.Date;
        var index = 1;

        while (weekStart <= end)
        {
            var weekEnd = weekStart.AddDays(6);
            if (weekEnd >= periodFrom.Date && weekStart <= end)
            {
                weeks.Add(index);
            }

            index++;
            weekStart = weekStart.AddDays(7);
        }

        return weeks;
    }

    public static int GetPeriodWeekIndex(DateTime periodFrom, DateTime date)
    {
        var periodStartSunday = GetSundayWeekStart(periodFrom);
        var weekStart = GetSundayWeekStart(date);
        if (weekStart < periodStartSunday)
        {
            return 1;
        }

        return (weekStart - periodStartSunday).Days / 7 + 1;
    }

    public static (DateTime WeekStart, DateTime WeekEnd) GetPeriodWeekDateRange(
        DateTime periodFrom,
        int periodWeekIndex
    )
    {
        var periodStartSunday = GetSundayWeekStart(periodFrom);
        var weekStart = periodStartSunday.AddDays((periodWeekIndex - 1) * 7);
        return (weekStart, weekStart.AddDays(6));
    }

    public static HashSet<int> GetIsoWeekNumbersOverlappingPeriodWeek(
        DateTime periodFrom,
        int periodWeekIndex
    )
    {
        var (weekStart, weekEnd) = GetPeriodWeekDateRange(periodFrom, periodWeekIndex);
        var isoWeeks = new HashSet<int>();
        for (var d = weekStart; d <= weekEnd; d = d.AddDays(1))
        {
            isoWeeks.Add(ISOWeek.GetWeekOfYear(d));
        }

        return isoWeeks;
    }

    /// <summary>
    /// Returns true when a constraint's week scope applies to batch scheduling week
    /// <paramref name="schedulingWeekNum"/> (period index). Supports constraints stored as
    /// either period week index or legacy ISO week number.
    /// </summary>
    public static bool ConstraintWeekApplies(
        int? constraintWeekNum,
        int schedulingWeekNum,
        DateTime? periodFrom
    )
    {
        if (!constraintWeekNum.HasValue)
        {
            return true;
        }

        if (!periodFrom.HasValue)
        {
            return constraintWeekNum.Value == schedulingWeekNum;
        }

        if (constraintWeekNum.Value == schedulingWeekNum)
        {
            return true;
        }

        return GetIsoWeekNumbersOverlappingPeriodWeek(periodFrom.Value, schedulingWeekNum)
            .Contains(constraintWeekNum.Value);
    }
}
