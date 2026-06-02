using Chronos.Engine.Matching;

namespace Chronos.Tests.Engine.TestFixtures;

public static class SchedulingWeekHelper
{
    public static List<int> GetPeriodWeekNumbers(DateTime fromDate, DateTime toDate) =>
        PeriodWeekCalculator.GetPeriodWeekIndices(fromDate, toDate).ToList();

    public static int GetPeriodWeekIndex(DateTime periodFrom, DateTime date) =>
        PeriodWeekCalculator.GetPeriodWeekIndex(periodFrom, date);

    /// <summary>Legacy alias — period week indices, not ISO week numbers.</summary>
    public static List<int> GetIsoWeekNumbers(DateTime fromDate, DateTime toDate) =>
        GetPeriodWeekNumbers(fromDate, toDate);
}
