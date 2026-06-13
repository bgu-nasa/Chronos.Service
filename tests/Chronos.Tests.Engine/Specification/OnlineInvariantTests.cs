using System.Globalization;
using Chronos.Domain.Schedule;
using Chronos.Domain.Schedule.Messages;
using Chronos.Tests.Engine.TestFixtures;

namespace Chronos.Tests.Engine.Specification;

[TestFixture]
[Category("Specification")]
[Category("TDD")]
public class OnlineInvariantTests
{
    private EngineIntegrationFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new EngineIntegrationFixture();

    [TearDown]
    public void TearDown() => _fixture.Dispose();

    [Test]
    public async Task WeekScopedConstraintChange_onlyAffectedWeekModified()
    {
        var activityId = Guid.NewGuid();
        var slot1 = Guid.NewGuid();
        var slot2 = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var week10 = ISOWeek.GetWeekOfYear(new DateTime(2026, 3, 2));
        var week11 = ISOWeek.GetWeekOfYear(new DateTime(2026, 3, 9));
        var week12 = ISOWeek.GetWeekOfYear(new DateTime(2026, 3, 16));

        var scenario = SchedulingScenarioBuilder.Create()
            .WithThreeWeekPeriod()
            .WithActivity(id: activityId, durationMinutes: 60)
            .WithSlot("Monday", 9, 10, id: slot1)
            .WithSlot("Tuesday", 9, 10, id: slot2)
            .WithResource("Room A", id: resourceId)
            .WithSeedAssignment(activityId, slot1, resourceId, week10)
            .WithSeedAssignment(activityId, slot1, resourceId, week11)
            .WithSeedAssignment(activityId, slot1, resourceId, week12)
            .Build();

        var constraintId = Guid.NewGuid();
        scenario.ActivityConstraints.Add(new ActivityConstraint
        {
            Id = constraintId,
            OrganizationId = scenario.OrganizationId,
            ActivityId = activityId,
            Key = "forbidden_timerange",
            Value = "Monday 09:00 - 10:00",
            WeekNum = week11,
        });

        _fixture.LoadScenario(scenario);
        _fixture.ActivityConstraintRepository.GetByActivityIdAsync(activityId)
            .Returns(scenario.ActivityConstraints);

        var before = _fixture.AssignmentRepository.Assignments
            .Where(a => a.ActivityId == activityId)
            .ToList();

        await _fixture.OnlineStrategy.ExecuteAsync(
            new HandleConstraintChangeRequest(
                constraintId,
                scenario.OrganizationId,
                scenario.PeriodId,
                ConstraintScope.Activity,
                ConstraintChangeOperation.Created,
                activityId
            ),
            CancellationToken.None
        );

        var after = _fixture.AssignmentRepository.Assignments
            .Where(a => a.ActivityId == activityId)
            .ToList();

        var week10Before = before.Where(a => a.WeekNum == week10).Select(a => a.SlotId).ToList();
        var week10After = after.Where(a => a.WeekNum == week10).Select(a => a.SlotId).ToList();
        week10After.Should().BeEquivalentTo(week10Before, "week 10 must be unchanged");

        var week12Before = before.Where(a => a.WeekNum == week12).Select(a => a.SlotId).ToList();
        var week12After = after.Where(a => a.WeekNum == week12).Select(a => a.SlotId).ToList();
        week12After.Should().BeEquivalentTo(week12Before, "week 12 must be unchanged");
    }

    [Test]
    public async Task ValidStreak_unchanged_otherWeeks_whenConstraintAddedToDifferentWeek()
    {
        var activityId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var week10 = ISOWeek.GetWeekOfYear(new DateTime(2026, 3, 2));
        var week12 = ISOWeek.GetWeekOfYear(new DateTime(2026, 3, 16));
        var forbiddenWeek = ISOWeek.GetWeekOfYear(new DateTime(2026, 3, 9));

        var scenario = SchedulingScenarioBuilder.Create()
            .WithThreeWeekPeriod()
            .WithActivity(id: activityId)
            .WithSlot("Monday", 9, 10, id: slotId)
            .WithResource("Room A", id: resourceId)
            .WithSeedAssignment(activityId, slotId, resourceId, week10)
            .WithSeedAssignment(activityId, slotId, resourceId, week12)
            .Build();

        _fixture.LoadScenario(scenario);
        var countBefore = _fixture.AssignmentRepository.Assignments.Count;

        var constraintId = Guid.NewGuid();
        _fixture.DbContext.ActivityConstraints.Add(new ActivityConstraint
        {
            Id = constraintId,
            OrganizationId = scenario.OrganizationId,
            ActivityId = activityId,
            Key = "forbidden_timerange",
            Value = "Monday 09:00 - 10:00",
            WeekNum = forbiddenWeek,
        });
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.ActivityConstraintRepository.GetByActivityIdAsync(activityId)
            .Returns(_fixture.DbContext.ActivityConstraints
                .Where(c => c.ActivityId == activityId).ToList());

        await _fixture.OnlineStrategy.ExecuteAsync(
            new HandleConstraintChangeRequest(
                constraintId,
                scenario.OrganizationId,
                scenario.PeriodId,
                ConstraintScope.Activity,
                ConstraintChangeOperation.Created,
                activityId
            ),
            CancellationToken.None
        );

        _fixture.AssignmentRepository.Assignments
            .Where(a => a.ActivityId == activityId && a.WeekNum == week10)
            .Should()
            .HaveCount(1);
        _fixture.AssignmentRepository.Assignments
            .Where(a => a.ActivityId == activityId && a.WeekNum == week12)
            .Should()
            .HaveCount(1);
    }

    [Test]
    public async Task InvalidStreak_afterOnlineRun_hasNoPartialRows_orIsFullyRemoved()
    {
        var activityId = Guid.NewGuid();
        var slot1 = Guid.NewGuid();
        var slot2 = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var week = ISOWeek.GetWeekOfYear(new DateTime(2026, 3, 2));

        var scenario = SchedulingScenarioBuilder.Create()
            .WithPeriod(new DateTime(2026, 3, 2), new DateTime(2026, 3, 8))
            .WithActivity(id: activityId, durationMinutes: 120)
            .WithSlot("Monday", 9, 10, id: slot1)
            .WithSlot("Monday", 10, 11, id: slot2)
            .WithResource("Room A", id: resourceId)
            .WithSeedAssignment(activityId, slot1, resourceId, week)
            .Build();

        _fixture.LoadScenario(scenario);

        var constraintId = Guid.NewGuid();
        _fixture.DbContext.ActivityConstraints.Add(new ActivityConstraint
        {
            Id = constraintId,
            OrganizationId = scenario.OrganizationId,
            ActivityId = activityId,
            Key = "forbidden_timerange",
            Value = "Monday 09:00 - 11:00",
            WeekNum = week,
        });
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.ActivityConstraintRepository.GetByActivityIdAsync(activityId)
            .Returns(_fixture.DbContext.ActivityConstraints
                .Where(c => c.ActivityId == activityId).ToList());

        await _fixture.OnlineStrategy.ExecuteAsync(
            new HandleConstraintChangeRequest(
                constraintId,
                scenario.OrganizationId,
                scenario.PeriodId,
                ConstraintScope.Activity,
                ConstraintChangeOperation.Created,
                activityId
            ),
            CancellationToken.None
        );

        var rows = _fixture.AssignmentRepository.Assignments
            .Where(a => a.ActivityId == activityId && a.WeekNum == week)
            .ToList();

        rows.Should().NotHaveCount(1, "I8: must not leave a partial streak (1 of 2 slots)");
    }

    [Test]
    public async Task UserScopeConstraint_reevaluatesAllTeacherActivities()
    {
        var teacherId = Guid.NewGuid();
        var activity1 = Guid.NewGuid();
        var activity2 = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var week = ISOWeek.GetWeekOfYear(new DateTime(2026, 3, 2));

        var tuesdaySlotId = Guid.NewGuid();
        var scenario = SchedulingScenarioBuilder.Create()
            .WithPeriod(new DateTime(2026, 3, 2), new DateTime(2026, 3, 8))
            .WithActivity(id: activity1, teacherId: teacherId)
            .WithActivity(id: activity2, teacherId: teacherId)
            .WithSlot("Monday", 9, 10, id: slotId)
            .WithSlot("Tuesday", 9, 10, id: tuesdaySlotId)
            .WithResource("Room A", id: resourceId)
            .WithSeedAssignment(activity1, slotId, resourceId, week)
            .WithSeedAssignment(activity2, tuesdaySlotId, resourceId, week)
            .Build();

        var constraintId = Guid.NewGuid();
        scenario.UserConstraints.Add(new UserConstraint
        {
            Id = constraintId,
            OrganizationId = scenario.OrganizationId,
            UserId = teacherId,
            SchedulingPeriodId = scenario.PeriodId,
            Key = "forbidden_timerange",
            Value = "Monday 09:00 - 10:00",
        });

        _fixture.LoadScenario(scenario);
        _fixture.UserConstraintRepository
            .GetByUserPeriodAsync(teacherId, scenario.PeriodId)
            .Returns(scenario.UserConstraints);

        await _fixture.OnlineStrategy.ExecuteAsync(
            new HandleConstraintChangeRequest(
                constraintId,
                scenario.OrganizationId,
                scenario.PeriodId,
                ConstraintScope.User,
                ConstraintChangeOperation.Created,
                UserId: teacherId
            ),
            CancellationToken.None
        );

        var violations = SchedulingInvariantVerifier.Verify(
            _fixture.AssignmentRepository.Assignments,
            scenario.Activities,
            _fixture.SlotsById,
            scenario.PeriodFrom,
            scenario.PeriodTo
        );

        violations.Where(v => v.StartsWith("I4:")).Should().BeEmpty();
    }

    [Test]
    public async Task Reschedule_preservesTeacherUniqueness()
    {
        var teacherId = Guid.NewGuid();
        var activity1 = Guid.NewGuid();
        var activity2 = Guid.NewGuid();
        var slot1 = Guid.NewGuid();
        var slot2 = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var week = ISOWeek.GetWeekOfYear(new DateTime(2026, 3, 2));

        var scenario = SchedulingScenarioBuilder.Create()
            .WithPeriod(new DateTime(2026, 3, 2), new DateTime(2026, 3, 8))
            .WithActivity(id: activity1, teacherId: teacherId)
            .WithActivity(id: activity2, teacherId: teacherId)
            .WithSlot("Monday", 9, 10, id: slot1)
            .WithSlot("Tuesday", 9, 10, id: slot2)
            .WithResource("Room A", id: resourceId)
            .WithSeedAssignment(activity1, slot1, resourceId, week)
            .WithSeedAssignment(activity2, slot2, resourceId, week)
            .Build();

        var constraintId = Guid.NewGuid();
        scenario.ActivityConstraints.Add(new ActivityConstraint
        {
            Id = constraintId,
            OrganizationId = scenario.OrganizationId,
            ActivityId = activity1,
            Key = "forbidden_timerange",
            Value = "Monday 09:00 - 10:00",
        });

        _fixture.LoadScenario(scenario);
        _fixture.ActivityConstraintRepository.GetByActivityIdAsync(activity1)
            .Returns(scenario.ActivityConstraints.Where(c => c.ActivityId == activity1).ToList());
        _fixture.ActivityConstraintRepository.GetByActivityIdAsync(activity2)
            .Returns([]);

        await _fixture.OnlineStrategy.ExecuteAsync(
            new HandleConstraintChangeRequest(
                constraintId,
                scenario.OrganizationId,
                scenario.PeriodId,
                ConstraintScope.Activity,
                ConstraintChangeOperation.Created,
                activity1
            ),
            CancellationToken.None
        );

        var violations = SchedulingInvariantVerifier.Verify(
            _fixture.AssignmentRepository.Assignments,
            scenario.Activities,
            _fixture.SlotsById,
            scenario.PeriodFrom,
            scenario.PeriodTo
        );

        violations.Where(v => v.StartsWith("I4:")).Should().BeEmpty();
    }
}
