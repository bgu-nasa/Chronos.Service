using System.Globalization;
using Chronos.Engine.Matching;

namespace Chronos.Tests.Engine.Specification;

[TestFixture]
[Category("Specification")]
public class PeriodWeekCalculatorTests
{
    [Test]
    public void SundayAndMondayInSameCalendarWeek_sharePeriodWeekIndex_notIsoWeek()
    {
        var periodFrom = new DateTime(2026, 6, 1);
        var sunday = new DateTime(2026, 6, 21);
        var monday = new DateTime(2026, 6, 22);

        var isoSunday = ISOWeek.GetWeekOfYear(sunday);
        var isoMonday = ISOWeek.GetWeekOfYear(monday);
        isoSunday.Should().NotBe(isoMonday, "ISO weeks split Sunday vs Monday at year boundaries");

        var periodWeekSunday = PeriodWeekCalculator.GetPeriodWeekIndex(periodFrom, sunday);
        var periodWeekMonday = PeriodWeekCalculator.GetPeriodWeekIndex(periodFrom, monday);
        periodWeekSunday.Should().Be(periodWeekMonday,
            "calendar week view uses one period week index for Sun–Sat");
    }
}
