using Chronos.Domain.Schedule.Messages;
using Chronos.Tests.Engine.TestFixtures;

namespace Chronos.Tests.Engine.Specification;

[TestFixture]
[Category("Specification")]
[Category("TDD")]
public class BatchSpreadTests
{
    private EngineIntegrationFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new EngineIntegrationFixture();

    [TearDown]
    public void TearDown() => _fixture.Dispose();

    [Test]
    public async Task ManyActivities_onePeriodWeek_spreadAcrossAtLeastThreeWeekdays()
    {
        var periodFrom = new DateTime(2026, 3, 2);
        var periodTo = new DateTime(2026, 3, 8);
        var builder = SchedulingScenarioBuilder.Create()
            .WithPeriod(periodFrom, periodTo)
            .WithResource("Room A");

        foreach (var day in new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" })
        {
            builder = builder.WithSlot(day, 9, 10).WithSlot(day, 10, 11);
        }

        for (var i = 0; i < 6; i++)
        {
            builder = builder.WithActivity(durationMinutes: 60);
        }

        var scenario = builder.Build();
        _fixture.LoadScenario(scenario);

        await _fixture.BatchStrategy.ExecuteAsync(
            new SchedulePeriodRequest(_fixture.PeriodId, _fixture.OrganizationId),
            CancellationToken.None
        );

        var periodWeek = SchedulingWeekHelper.GetPeriodWeekIndex(periodFrom, periodFrom);
        var weekdaysUsed = _fixture.AssignmentRepository.Assignments
            .Where(a => a.WeekNum == periodWeek)
            .Select(a => _fixture.SlotsById[a.SlotId].Weekday)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        weekdaysUsed.Count.Should().BeGreaterThanOrEqualTo(3,
            $"activities should spread across weekdays, got: [{string.Join(", ", weekdaysUsed)}]");
    }
}
