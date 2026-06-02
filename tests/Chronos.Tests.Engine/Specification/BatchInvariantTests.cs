using Chronos.Domain.Schedule.Messages;
using Chronos.Tests.Engine.TestFixtures;

namespace Chronos.Tests.Engine.Specification;

[TestFixture]
[Category("Specification")]
[Category("TDD")]
public class BatchInvariantTests
{
    private EngineIntegrationFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new EngineIntegrationFixture();

    [TearDown]
    public void TearDown() => _fixture.Dispose();

    [Test]
    public async Task SingleActivity_threeWeekPeriod_hasAssignmentEveryWeek_orFullyUnscheduled()
    {
        var activityId = Guid.NewGuid();
        var scenario = SchedulingScenarioBuilder.Create()
            .WithThreeWeekPeriod()
            .WithActivity(id: activityId, durationMinutes: 60)
            .WithSlot("Monday", 9, 10)
            .WithResource("Room A", capacity: 40)
            .Build();

        _fixture.LoadScenario(scenario);
        var result = await _fixture.BatchStrategy.ExecuteAsync(
            new SchedulePeriodRequest(_fixture.PeriodId, _fixture.OrganizationId),
            CancellationToken.None
        );

        var activity = scenario.Activities[0];
        var completeness = SchedulingInvariantVerifier.VerifyPerWeekCompleteness(
            _fixture.AssignmentRepository.Assignments,
            activity,
            scenario.PeriodFrom,
            scenario.PeriodTo
        );

        completeness.Should().BeEmpty(
            $"expected all ISO weeks covered or none:\n{string.Join("\n", completeness)}"
        );
    }

    [Test]
    public async Task MultiSlotActivity_eachWeek_hasExactlyKRows()
    {
        var activityId = Guid.NewGuid();
        var scenario = SchedulingScenarioBuilder.Create()
            .WithThreeWeekPeriod()
            .WithActivity(id: activityId, durationMinutes: 120)
            .WithConsecutiveSlots("Monday", (9, 10), (10, 11))
            .WithResource("Room A")
            .Build();

        _fixture.LoadScenario(scenario);
        await _fixture.BatchStrategy.ExecuteAsync(
            new SchedulePeriodRequest(_fixture.PeriodId, _fixture.OrganizationId),
            CancellationToken.None
        );

        var requiredWeeks = SchedulingWeekHelper.GetIsoWeekNumbers(scenario.PeriodFrom, scenario.PeriodTo);
        var violations = SchedulingInvariantVerifier.Verify(
            _fixture.AssignmentRepository.Assignments,
            scenario.Activities,
            _fixture.SlotsById,
            scenario.PeriodFrom,
            scenario.PeriodTo
        );

        violations.Should().BeEmpty(string.Join("\n", violations));

        foreach (var week in requiredWeeks)
        {
            var rows = _fixture.AssignmentRepository.Assignments
                .Where(a => a.ActivityId == activityId && a.WeekNum == week)
                .ToList();

            if (rows.Count > 0)
                rows.Should().HaveCount(2, $"week {week} should have 2 slot rows for 120-min activity");
        }
    }

    [Test]
    public async Task TwoActivities_sameTeacher_overlappingSlot_sameWeek_notBothScheduled()
    {
        var teacherId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var resource1 = Guid.NewGuid();
        var resource2 = Guid.NewGuid();
        var activity1 = Guid.NewGuid();
        var activity2 = Guid.NewGuid();

        var scenario = SchedulingScenarioBuilder.Create()
            .WithPeriod(new DateTime(2026, 3, 2), new DateTime(2026, 3, 8))
            .WithActivity(id: activity1, teacherId: teacherId)
            .WithActivity(id: activity2, teacherId: teacherId)
            .WithSlot("Monday", 9, 10, id: slotId)
            .WithResource("Room A", id: resource1)
            .WithResource("Room B", id: resource2)
            .Build();

        _fixture.LoadScenario(scenario);
        await _fixture.BatchStrategy.ExecuteAsync(
            new SchedulePeriodRequest(_fixture.PeriodId, _fixture.OrganizationId),
            CancellationToken.None
        );

        var violations = SchedulingInvariantVerifier.Verify(
            _fixture.AssignmentRepository.Assignments,
            scenario.Activities,
            _fixture.SlotsById,
            scenario.PeriodFrom,
            scenario.PeriodTo
        );

        violations.Where(v => v.StartsWith("I4:")).Should().BeEmpty(
            "I4 teacher exclusivity:\n" + string.Join("\n", violations.Where(v => v.StartsWith("I4:"))));
    }

    [Test]
    public async Task TwoActivities_contestedSlotRoom_onlyOneGetsSlot()
    {
        var slotId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var scenario = SchedulingScenarioBuilder.Create()
            .WithPeriod(new DateTime(2026, 3, 2), new DateTime(2026, 3, 8))
            .WithActivity()
            .WithActivity()
            .WithSlot("Monday", 9, 10, id: slotId)
            .WithResource("Room A", id: resourceId)
            .Build();

        _fixture.LoadScenario(scenario);
        await _fixture.BatchStrategy.ExecuteAsync(
            new SchedulePeriodRequest(_fixture.PeriodId, _fixture.OrganizationId),
            CancellationToken.None
        );

        var week = SchedulingWeekHelper.GetIsoWeekNumbers(scenario.PeriodFrom, scenario.PeriodTo)[0];
        var atSlot = _fixture.AssignmentRepository.Assignments
            .Where(a => a.SlotId == slotId && a.ResourceId == resourceId && a.WeekNum == week)
            .ToList();

        atSlot.Should().HaveCountLessThanOrEqualTo(1, "I3: at most one activity per (slot, room, week)");
    }

    [Test]
    public async Task Result_unscheduledImpliesNoAssignmentRows_anyWeek()
    {
        var activityId = Guid.NewGuid();
        var scenario = SchedulingScenarioBuilder.Create()
            .WithThreeWeekPeriod()
            .WithActivity(id: activityId)
            .WithSlot("Monday", 9, 10)
            .WithResource("Room A")
            .Build();

        _fixture.LoadScenario(scenario);
        var result = await _fixture.BatchStrategy.ExecuteAsync(
            new SchedulePeriodRequest(_fixture.PeriodId, _fixture.OrganizationId),
            CancellationToken.None
        );

        if (result.UnscheduledActivityIds.Contains(activityId))
        {
            var requiredWeeks = SchedulingWeekHelper.GetIsoWeekNumbers(
                scenario.PeriodFrom,
                scenario.PeriodTo
            );
            var weeksPresent = _fixture.AssignmentRepository.Assignments
                .Where(a => a.ActivityId == activityId && a.WeekNum.HasValue)
                .Select(a => a.WeekNum!.Value)
                .Distinct()
                .ToHashSet();

            requiredWeeks.Any(w => !weeksPresent.Contains(w))
                .Should()
                .BeTrue("unscheduled activity must be missing at least one required week");
        }
        else
        {
            var reportingViolations = SchedulingInvariantVerifier.Verify(
                _fixture.AssignmentRepository.Assignments,
                scenario.Activities,
                _fixture.SlotsById,
                scenario.PeriodFrom,
                scenario.PeriodTo,
                result.UnscheduledActivityIds
            );
            reportingViolations.Where(v => v.Contains("UnscheduledActivityIds")).Should().BeEmpty();
        }
    }

    [Test]
    public async Task Result_successImpliesNoPartialStreaks()
    {
        var scenario = SchedulingScenarioBuilder.Create()
            .WithThreeWeekPeriod()
            .WithActivity(durationMinutes: 120)
            .WithConsecutiveSlots("Monday", (9, 10), (10, 11))
            .WithResource("Room A")
            .Build();

        _fixture.LoadScenario(scenario);
        var result = await _fixture.BatchStrategy.ExecuteAsync(
            new SchedulePeriodRequest(_fixture.PeriodId, _fixture.OrganizationId),
            CancellationToken.None
        );

        result.Success.Should().BeTrue();

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

    [Test]
    public async Task Period_partialWeek_noWednesdaySlots_notAssignedOnWednesday()
    {
        // Period starts Thursday 2026-03-05 — ISO week 10 has no Monday–Wednesday in range for first partial slice
        var wedSlotId = Guid.NewGuid();
        var monSlotId = Guid.NewGuid();
        var scenario = SchedulingScenarioBuilder.Create()
            .WithPeriod(new DateTime(2026, 3, 5), new DateTime(2026, 3, 8))
            .WithActivity()
            .WithSlot("Wednesday", 9, 10, id: wedSlotId)
            .WithSlot("Thursday", 9, 10, id: monSlotId)
            .WithResource("Room A")
            .Build();

        _fixture.LoadScenario(scenario);
        await _fixture.BatchStrategy.ExecuteAsync(
            new SchedulePeriodRequest(_fixture.PeriodId, _fixture.OrganizationId),
            CancellationToken.None
        );

        var wedAssignments = _fixture.AssignmentRepository.Assignments
            .Where(a => a.SlotId == wedSlotId)
            .ToList();

        wedAssignments.Should().BeEmpty(
            "eligible-slot rule: Wednesday template must not be used when week has no Wednesday in period range"
        );
    }

    [Test]
    public async Task Activity_weekScopedForbidden_onlyThatWeekUnscheduled()
    {
        var activityId = Guid.NewGuid();
        var periodFrom = new DateTime(2026, 3, 2);
        var week11 = SchedulingWeekHelper.GetPeriodWeekIndex(periodFrom, new DateTime(2026, 3, 9));
        var scenario = SchedulingScenarioBuilder.Create()
            .WithThreeWeekPeriod()
            .WithActivity(id: activityId)
            .WithConsecutiveSlots("Monday", (9, 10), (10, 11))
            .WithResource("Room A")
            .WithActivityConstraint(
                activityId,
                "forbidden_timerange",
                "Monday 09:00 - 11:00",
                weekNum: week11
            )
            .Build();

        _fixture.LoadScenario(scenario);
        await _fixture.BatchStrategy.ExecuteAsync(
            new SchedulePeriodRequest(_fixture.PeriodId, _fixture.OrganizationId),
            CancellationToken.None
        );

        var week11Rows = _fixture.AssignmentRepository.Assignments
            .Where(a => a.ActivityId == activityId && a.WeekNum == week11)
            .ToList();

        week11Rows.Should().BeEmpty($"week {week11} should be unscheduled due to forbidden_timerange");

        var otherWeekRows = _fixture.AssignmentRepository.Assignments
            .Where(a => a.ActivityId == activityId && a.WeekNum != week11)
            .ToList();

        if (otherWeekRows.Count > 0)
        {
            var violations = SchedulingInvariantVerifier.Verify(
                _fixture.AssignmentRepository.Assignments,
                scenario.Activities,
                _fixture.SlotsById,
                scenario.PeriodFrom,
                scenario.PeriodTo
            );
            violations.Should().BeEmpty(string.Join("\n", violations));
        }
    }

    [Test]
    public async Task Activity_forbiddenPreferredSlotInOneWeek_schedulesAlternateSlotThatWeek()
    {
        var activityId = Guid.NewGuid();
        var mondaySlotId = Guid.NewGuid();
        var tuesdaySlotId = Guid.NewGuid();
        var periodFrom = new DateTime(2026, 3, 2);
        var week11 = SchedulingWeekHelper.GetPeriodWeekIndex(periodFrom, new DateTime(2026, 3, 9));

        var scenario = SchedulingScenarioBuilder.Create()
            .WithThreeWeekPeriod()
            .WithActivity(id: activityId)
            .WithSlot("Monday", 9, 10, id: mondaySlotId)
            .WithSlot("Tuesday", 9, 10, id: tuesdaySlotId)
            .WithResource("Room A")
            .WithActivityConstraint(
                activityId,
                "forbidden_timerange",
                "Monday 09:00 - 10:00",
                weekNum: week11
            )
            .Build();

        _fixture.LoadScenario(scenario);
        await _fixture.BatchStrategy.ExecuteAsync(
            new SchedulePeriodRequest(_fixture.PeriodId, _fixture.OrganizationId),
            CancellationToken.None
        );

        var completeness = SchedulingInvariantVerifier.VerifyPerWeekCompleteness(
            _fixture.AssignmentRepository.Assignments,
            scenario.Activities[0],
            scenario.PeriodFrom,
            scenario.PeriodTo
        );

        completeness.Should().BeEmpty(
            "every required week should be scheduled when an alternate slot exists:\n"
            + string.Join("\n", completeness)
        );

        var week11Slot = _fixture.AssignmentRepository.Assignments
            .Where(a => a.ActivityId == activityId && a.WeekNum == week11)
            .Select(a => a.SlotId)
            .FirstOrDefault();

        week11Slot.Should().Be(tuesdaySlotId, "week with forbidden Monday should use Tuesday");
    }

    [Test]
    public async Task InsufficientCapacity_unscheduledAllWeeks()
    {
        var activityId = Guid.NewGuid();
        var scenario = SchedulingScenarioBuilder.Create()
            .WithThreeWeekPeriod()
            .WithActivity(id: activityId, expectedStudents: 50)
            .WithSlot("Monday", 9, 10)
            .WithResource("Small Room", capacity: 10)
            .WithActivityConstraint(activityId, "required_capacity", """{"min": 40}""")
            .Build();

        _fixture.LoadScenario(scenario);
        var result = await _fixture.BatchStrategy.ExecuteAsync(
            new SchedulePeriodRequest(_fixture.PeriodId, _fixture.OrganizationId),
            CancellationToken.None
        );

        result.UnscheduledActivityIds.Should().Contain(activityId);
        _fixture.AssignmentRepository.Assignments.Should().BeEmpty();
    }

    [Test]
    public async Task ExpectedStudents_exceedsRoomCapacity_withoutRequiredCapacityConstraint_unscheduled()
    {
        var activityId = Guid.NewGuid();
        var smallRoomId = Guid.NewGuid();

        var scenario = SchedulingScenarioBuilder.Create()
            .WithPeriod(new DateTime(2026, 3, 2), new DateTime(2026, 3, 8))
            .WithActivity(id: activityId, expectedStudents: 30)
            .WithSlot("Monday", 9, 10)
            .WithResource("Small Room", id: smallRoomId, capacity: 15)
            .Build();

        _fixture.LoadScenario(scenario);
        var result = await _fixture.BatchStrategy.ExecuteAsync(
            new SchedulePeriodRequest(_fixture.PeriodId, _fixture.OrganizationId),
            CancellationToken.None
        );

        result.UnscheduledActivityIds.Should().Contain(activityId);
        _fixture.AssignmentRepository.Assignments
            .Where(a => a.ActivityId == activityId && a.ResourceId == smallRoomId)
            .Should()
            .BeEmpty("30 students must not be placed in a 15-seat room without required_capacity row");
    }
}
