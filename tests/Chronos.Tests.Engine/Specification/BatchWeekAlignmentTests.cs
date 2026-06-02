using Chronos.Domain.Schedule.Messages;
using Chronos.Engine.Matching;
using Chronos.Tests.Engine.TestFixtures;

namespace Chronos.Tests.Engine.Specification;

[TestFixture]
[Category("Specification")]
[Category("TDD")]
public class BatchWeekAlignmentTests
{
    private EngineIntegrationFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new EngineIntegrationFixture();

    [TearDown]
    public void TearDown() => _fixture.Dispose();

    [Test]
    public async Task SingleActivity_periodWeekIndex_atMostOneWeekdayPerPeriodWeek()
    {
        var activityId = Guid.NewGuid();
        var periodFrom = new DateTime(2026, 6, 1);
        var periodTo = new DateTime(2026, 6, 30);

        var scenario = SchedulingScenarioBuilder.Create()
            .WithPeriod(periodFrom, periodTo)
            .WithActivity(id: activityId, durationMinutes: 120)
            .WithConsecutiveSlots("Monday", (9, 10), (10, 11))
            .WithConsecutiveSlots("Tuesday", (9, 10), (10, 11))
            .WithConsecutiveSlots("Wednesday", (9, 10), (10, 11))
            .WithResource("Room A")
            .Build();

        _fixture.LoadScenario(scenario);
        await _fixture.BatchStrategy.ExecuteAsync(
            new SchedulePeriodRequest(_fixture.PeriodId, _fixture.OrganizationId),
            CancellationToken.None
        );

        var periodWeek3 = PeriodWeekCalculator.GetPeriodWeekIndex(periodFrom, new DateTime(2026, 6, 21));
        var rowsWeek3 = _fixture.AssignmentRepository.Assignments
            .Where(a => a.ActivityId == activityId && a.WeekNum == periodWeek3)
            .ToList();

        rowsWeek3.Should().NotBeEmpty();
        var weekdays = rowsWeek3
            .Select(a => _fixture.SlotsById[a.SlotId].Weekday)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        weekdays.Should().HaveCount(1,
            "one activity must not appear on two weekdays in the same period week (fixes Sun+Mon duplicate display)");
    }
}
