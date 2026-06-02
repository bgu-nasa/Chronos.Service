using Chronos.Domain.Schedule.Messages;
using Chronos.Tests.Engine.TestFixtures;

namespace Chronos.Tests.Engine.Specification;

[TestFixture]
[Category("Specification")]
[Category("TDD")]
public class BatchCrossWeekTests
{
    private EngineIntegrationFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new EngineIntegrationFixture();

    [TearDown]
    public void TearDown() => _fixture.Dispose();

    [Test]
    public async Task SameActivity_crossWeeks_sameSlotTemplates_whenFeasible()
    {
        var activityId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        var scenario = SchedulingScenarioBuilder.Create()
            .WithThreeWeekPeriod()
            .WithActivity(id: activityId)
            .WithSlot("Monday", 9, 10, id: slotId)
            .WithResource("Room A", id: resourceId)
            .Build();

        _fixture.LoadScenario(scenario);
        await _fixture.BatchStrategy.ExecuteAsync(
            new SchedulePeriodRequest(_fixture.PeriodId, _fixture.OrganizationId),
            CancellationToken.None
        );

        var byWeek = _fixture.AssignmentRepository.Assignments
            .Where(a => a.ActivityId == activityId)
            .GroupBy(a => a.WeekNum)
            .Where(g => g.Any())
            .ToList();

        byWeek.Should().HaveCountGreaterThan(1, "activity should recur in multiple weeks");

        var slotTemplates = byWeek
            .Select(g => (g.Key, SlotId: g.First().SlotId, ResourceId: g.First().ResourceId))
            .ToList();

        slotTemplates.Select(t => t.SlotId).Distinct().Should().HaveCount(1,
            "T1: same slot template across weeks when feasible");
    }

    [Test]
    public async Task RoomConflict_oneWeek_usesSameSlotsDifferentRoom_whenSecondRoomAvailable()
    {
        var activityId = Guid.NewGuid();
        var slot1 = Guid.NewGuid();
        var slot2 = Guid.NewGuid();
        var r1 = Guid.NewGuid();
        var r2 = Guid.NewGuid();
        var blockerId = Guid.NewGuid();

        var scenario = SchedulingScenarioBuilder.Create()
            .WithThreeWeekPeriod()
            .WithActivity(id: blockerId)
            .WithActivity(id: activityId)
            .WithConsecutiveSlots("Monday", (9, 10), (10, 11))
            .WithResource("Room 1", id: r1)
            .WithResource("Room 2", id: r2)
            .Build();

        _fixture.LoadScenario(scenario);
        await _fixture.BatchStrategy.ExecuteAsync(
            new SchedulePeriodRequest(_fixture.PeriodId, _fixture.OrganizationId),
            CancellationToken.None
        );

        var targetWeeks = _fixture.AssignmentRepository.Assignments
            .Where(a => a.ActivityId == activityId)
            .GroupBy(a => a.WeekNum)
            .ToList();

        targetWeeks.Should().NotBeEmpty();

        var slotSets = targetWeeks
            .Select(g => string.Join("|", g.OrderBy(a => a.SlotId).Select(a => a.SlotId)))
            .Distinct()
            .ToList();

        slotSets.Should().HaveCount(1,
            "T2: same slot templates across weeks even if room differs");
    }

    [Test]
    public async Task ShuffledWeekOrder_changesOutcome_notAlwaysFirstCalendarWeek()
    {
        var activityId = Guid.NewGuid();
        var scenario = SchedulingScenarioBuilder.Create()
            .WithThreeWeekPeriod()
            .WithActivity(id: activityId)
            .WithSlot("Monday", 9, 10)
            .WithSlot("Tuesday", 9, 10)
            .WithResource("Room A")
            .WithResource("Room B")
            .Build();

        var firstWeekSlots = new HashSet<Guid>();

        for (var run = 0; run < 15; run++)
        {
            _fixture.LoadScenario(scenario);
            await _fixture.BatchStrategy.ExecuteAsync(
                new SchedulePeriodRequest(_fixture.PeriodId, _fixture.OrganizationId),
                CancellationToken.None
            );

            var weeks = SchedulingWeekHelper.GetIsoWeekNumbers(scenario.PeriodFrom, scenario.PeriodTo);
            var firstWeek = weeks[0];
            var firstAssignment = _fixture.AssignmentRepository.Assignments
                .FirstOrDefault(a => a.ActivityId == activityId && a.WeekNum == firstWeek);

            if (firstAssignment != null)
                firstWeekSlots.Add(firstAssignment.SlotId);
        }

        firstWeekSlots.Count.Should().BeGreaterThan(1,
            "randomized processing should not always lock the calendar-first week's slot template");
    }

    [Test]
    public async Task NoHardLock_firstProcessedWeek_doesNotForceSameAnchorAcrossAllRuns()
    {
        var activityId = Guid.NewGuid();
        var monSlot = Guid.NewGuid();
        var tueSlot = Guid.NewGuid();

        var scenario = SchedulingScenarioBuilder.Create()
            .WithThreeWeekPeriod()
            .WithActivity(id: activityId)
            .WithSlot("Monday", 9, 10, id: monSlot)
            .WithSlot("Tuesday", 9, 10, id: tueSlot)
            .WithResource("Room A")
            .Build();

        var anchorsSeen = new HashSet<Guid>();

        for (var i = 0; i < 20; i++)
        {
            _fixture.LoadScenario(scenario);
            await _fixture.BatchStrategy.ExecuteAsync(
                new SchedulePeriodRequest(_fixture.PeriodId, _fixture.OrganizationId),
                CancellationToken.None
            );

            var anchor = _fixture.AssignmentRepository.Assignments
                .Where(a => a.ActivityId == activityId)
                .OrderBy(a => a.WeekNum)
                .Select(a => a.SlotId)
                .FirstOrDefault();

            if (anchor != Guid.Empty)
                anchorsSeen.Add(anchor);
        }

        anchorsSeen.Count.Should().BeGreaterThan(1,
            "×1000 previous-selection lock should not force a single anchor across batch runs");
    }

    [Test]
    public async Task CrossWeekAssignments_satisfyInvariants_whenScheduled()
    {
        var scenario = SchedulingScenarioBuilder.Create()
            .WithFiveWeekPeriod()
            .WithActivity(durationMinutes: 60)
            .WithActivity(durationMinutes: 120)
            .WithConsecutiveSlots("Monday", (9, 10), (10, 11))
            .WithConsecutiveSlots("Tuesday", (9, 10), (10, 11))
            .WithResource("Room A")
            .WithResource("Room B")
            .Build();

        _fixture.LoadScenario(scenario);
        var result = await _fixture.BatchStrategy.ExecuteAsync(
            new SchedulePeriodRequest(_fixture.PeriodId, _fixture.OrganizationId),
            CancellationToken.None
        );

        var violations = SchedulingInvariantVerifier.Verify(
            _fixture.AssignmentRepository.Assignments,
            scenario.Activities,
            _fixture.SlotsById,
            scenario.PeriodFrom,
            scenario.PeriodTo,
            result.UnscheduledActivityIds
        );

        violations.Should().BeEmpty(string.Join("\n", violations));
    }
}
