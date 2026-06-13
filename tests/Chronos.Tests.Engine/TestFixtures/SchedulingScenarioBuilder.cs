using Chronos.Data.Context;
using Chronos.Domain.Resources;
using Chronos.Domain.Schedule;

namespace Chronos.Tests.Engine.TestFixtures;

public sealed class SchedulingScenario
{
    public Guid OrganizationId { get; init; }
    public Guid PeriodId { get; init; }
    public Guid SubjectId { get; init; }
    public Guid DepartmentId { get; init; }
    public DateTime PeriodFrom { get; init; }
    public DateTime PeriodTo { get; init; }
    public List<Activity> Activities { get; init; } = [];
    public List<Slot> Slots { get; init; } = [];
    public List<Resource> Resources { get; init; } = [];
    public List<ActivityConstraint> ActivityConstraints { get; init; } = [];
    public List<UserConstraint> UserConstraints { get; init; } = [];
    public List<UserPreference> UserPreferences { get; init; } = [];
    public List<Assignment> SeedAssignments { get; init; } = [];
}

public sealed class SchedulingScenarioBuilder
{
    private Guid _orgId = Guid.NewGuid();
    private Guid _periodId = Guid.NewGuid();
    private Guid _subjectId = Guid.NewGuid();
    private Guid _departmentId = Guid.NewGuid();
    private DateTime _from = new(2026, 3, 2);
    private DateTime _to = new(2026, 3, 8);
    private readonly List<Activity> _activities = [];
    private readonly List<Slot> _slots = [];
    private readonly List<Resource> _resources = [];
    private readonly List<ActivityConstraint> _activityConstraints = [];
    private readonly List<UserConstraint> _userConstraints = [];
    private readonly List<UserPreference> _userPreferences = [];
    private readonly List<Assignment> _seedAssignments = [];

    public SchedulingScenarioBuilder WithOrganization(Guid orgId)
    {
        _orgId = orgId;
        return this;
    }

    public SchedulingScenarioBuilder WithPeriod(DateTime from, DateTime to)
    {
        _from = from;
        _to = to;
        return this;
    }

    public SchedulingScenarioBuilder WithThreeWeekPeriod() =>
        WithPeriod(new DateTime(2026, 3, 2), new DateTime(2026, 3, 15));

    public SchedulingScenarioBuilder WithFiveWeekPeriod() =>
        WithPeriod(new DateTime(2026, 3, 2), new DateTime(2026, 3, 29));

    public SchedulingScenarioBuilder WithActivity(
        Guid? id = null,
        int durationMinutes = 60,
        Guid? teacherId = null,
        int? expectedStudents = 30,
        string activityType = "Lecture"
    )
    {
        _activities.Add(new Activity
        {
            Id = id ?? Guid.NewGuid(),
            OrganizationId = _orgId,
            SubjectId = _subjectId,
            AssignedUserId = teacherId ?? Guid.NewGuid(),
            ActivityType = activityType,
            Duration = durationMinutes,
            ExpectedStudents = expectedStudents,
        });
        return this;
    }

    public SchedulingScenarioBuilder WithConsecutiveSlots(
        string weekday,
        params (int fromHour, int toHour)[] blocks
    )
    {
        TimeSpan? prevEnd = null;
        foreach (var (fromH, toH) in blocks)
        {
            var from = new TimeSpan(fromH, 0, 0);
            var to = new TimeSpan(toH, 0, 0);
            if (prevEnd.HasValue && prevEnd.Value != from)
                throw new ArgumentException("Slot blocks must be consecutive in time");

            _slots.Add(new Slot
            {
                Id = Guid.NewGuid(),
                OrganizationId = _orgId,
                SchedulingPeriodId = _periodId,
                Weekday = weekday,
                FromTime = from,
                ToTime = to,
            });
            prevEnd = to;
        }

        return this;
    }

    public SchedulingScenarioBuilder WithSlot(
        string weekday,
        int fromHour,
        int toHour,
        Guid? id = null
    )
    {
        _slots.Add(new Slot
        {
            Id = id ?? Guid.NewGuid(),
            OrganizationId = _orgId,
            SchedulingPeriodId = _periodId,
            Weekday = weekday,
            FromTime = new TimeSpan(fromHour, 0, 0),
            ToTime = new TimeSpan(toHour, 0, 0),
        });
        return this;
    }

    public SchedulingScenarioBuilder WithResource(
        string identifier,
        int? capacity = 40,
        Guid? id = null,
        Guid? resourceTypeId = null
    )
    {
        _resources.Add(new Resource
        {
            Id = id ?? Guid.NewGuid(),
            OrganizationId = _orgId,
            ResourceTypeId = resourceTypeId ?? Guid.NewGuid(),
            Location = "Building A",
            Identifier = identifier,
            Capacity = capacity,
        });
        return this;
    }

    public SchedulingScenarioBuilder WithActivityConstraint(
        Guid activityId,
        string key,
        string value,
        int? weekNum = null
    )
    {
        _activityConstraints.Add(new ActivityConstraint
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            ActivityId = activityId,
            Key = key,
            Value = value,
            WeekNum = weekNum,
        });
        return this;
    }

    public SchedulingScenarioBuilder WithUserConstraint(
        Guid userId,
        string key,
        string value,
        int? weekNum = null
    )
    {
        _userConstraints.Add(new UserConstraint
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            UserId = userId,
            SchedulingPeriodId = _periodId,
            Key = key,
            Value = value,
            WeekNum = weekNum,
        });
        return this;
    }

    public SchedulingScenarioBuilder WithSeedAssignment(
        Guid activityId,
        Guid slotId,
        Guid resourceId,
        int weekNum
    )
    {
        _seedAssignments.Add(new Assignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            ActivityId = activityId,
            SlotId = slotId,
            ResourceId = resourceId,
            WeekNum = weekNum,
        });
        return this;
    }

    public SchedulingScenario Build() => new()
    {
        OrganizationId = _orgId,
        PeriodId = _periodId,
        SubjectId = _subjectId,
        DepartmentId = _departmentId,
        PeriodFrom = _from,
        PeriodTo = _to,
        Activities = _activities,
        Slots = _slots,
        Resources = _resources,
        ActivityConstraints = _activityConstraints,
        UserConstraints = _userConstraints,
        UserPreferences = _userPreferences,
        SeedAssignments = _seedAssignments,
    };

    public static SchedulingScenarioBuilder Create() => new();
}

public static class SchedulingScenarioApplicator
{
    public static void ApplyToDbContext(SchedulingScenario scenario, AppDbContext db)
    {
        db.SchedulingPeriods.Add(new SchedulingPeriod
        {
            Id = scenario.PeriodId,
            OrganizationId = scenario.OrganizationId,
            Name = "Spec Test Period",
            FromDate = scenario.PeriodFrom,
            ToDate = scenario.PeriodTo,
        });
        db.Subjects.Add(new Subject
        {
            Id = scenario.SubjectId,
            OrganizationId = scenario.OrganizationId,
            DepartmentId = scenario.DepartmentId,
            SchedulingPeriodId = scenario.PeriodId,
            Code = "SPEC101",
            Name = "Specification Course",
        });
        db.Activities.AddRange(scenario.Activities);
        db.Slots.AddRange(scenario.Slots);
        db.Resources.AddRange(scenario.Resources);
        db.ActivityConstraints.AddRange(scenario.ActivityConstraints);
        db.UserConstraints.AddRange(scenario.UserConstraints);
        db.UserPreferences.AddRange(scenario.UserPreferences);
    }
}
