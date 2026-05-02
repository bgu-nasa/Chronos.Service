using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Schedule;
using Chronos.Engine.Constraints;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Chronos.Tests.Engine.Constraints;

[TestFixture]
[Category("Unit")]
public class ActivityConstraintProcessorTests
{
    private IActivityConstraintRepository _constraintRepo;
    private IUserConstraintRepository _userConstraintRepo;
    private ISlotRepository _slotRepo;
    private ActivityConstraintProcessor _sut;

    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _activityId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _periodId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _constraintRepo = Substitute.For<IActivityConstraintRepository>();
        _userConstraintRepo = Substitute.For<IUserConstraintRepository>();
        _slotRepo = Substitute.For<ISlotRepository>();

        _constraintRepo.GetByActivityIdAsync(_activityId)
            .Returns(new List<ActivityConstraint>());
        _userConstraintRepo.GetByUserPeriodAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns(new List<UserConstraint>());

        _sut = new ActivityConstraintProcessor(
            _constraintRepo,
            _userConstraintRepo,
            _slotRepo,
            Enumerable.Empty<IConstraintHandler>(),
            Substitute.For<ILogger<ActivityConstraintProcessor>>());
    }

    [Test]
    public async Task GivenNoConstraints_WhenGetExcludedSlots_ThenReturnsEmpty()
    {
        var result = await _sut.GetExcludedSlotIdsAsync(_activityId, _orgId);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GivenHandlerForConstraintKey_WhenGetExcludedSlots_ThenDelegatesToHandler()
    {
        var slotId = Guid.NewGuid();
        var constraint = MakeActivityConstraint("custom_constraint", "value");
        _constraintRepo.GetByActivityIdAsync(_activityId)
            .Returns(new List<ActivityConstraint> { constraint });

        var handler = Substitute.For<IConstraintHandler>();
        handler.ConstraintKey.Returns("custom_constraint");
        handler.ProcessConstraintAsync(constraint, _orgId)
            .Returns(new HashSet<Guid> { slotId });

        _sut = new ActivityConstraintProcessor(
            _constraintRepo, _userConstraintRepo, _slotRepo,
            new[] { handler },
            Substitute.For<ILogger<ActivityConstraintProcessor>>());

        var result = await _sut.GetExcludedSlotIdsAsync(_activityId, _orgId);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result, Does.Contain(slotId));
    }

    [Test]
    public async Task GivenUnknownConstraintKey_WhenGetExcludedSlots_ThenSkipsGracefully()
    {
        var constraint = MakeActivityConstraint("unknown_key", "value");
        _constraintRepo.GetByActivityIdAsync(_activityId)
            .Returns(new List<ActivityConstraint> { constraint });

        var result = await _sut.GetExcludedSlotIdsAsync(_activityId, _orgId);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GivenForbiddenTimeRange_WhenSlotOverlaps_ThenExcludesSlot()
    {
        var mondaySlot = MakeSlot("Monday", 9, 11);
        var tuesdaySlot = MakeSlot("Tuesday", 9, 11);

        _constraintRepo.GetByActivityIdAsync(_activityId)
            .Returns(new List<ActivityConstraint>
            {
                MakeActivityConstraint("forbidden_timerange", "Monday 08:00 - 12:00")
            });
        _slotRepo.GetAllAsync()
            .Returns(new List<Slot> { mondaySlot, tuesdaySlot });

        var result = await _sut.GetExcludedSlotIdsAsync(_activityId, _orgId);

        Assert.That(result, Does.Contain(mondaySlot.Id));
        Assert.That(result, Does.Not.Contain(tuesdaySlot.Id));
    }

    [Test]
    public async Task GivenForbiddenTimeRange_WhenSlotDoesNotOverlap_ThenNotExcluded()
    {
        var slot = MakeSlot("Monday", 14, 16);

        _constraintRepo.GetByActivityIdAsync(_activityId)
            .Returns(new List<ActivityConstraint>
            {
                MakeActivityConstraint("forbidden_timerange", "Monday 08:00 - 12:00")
            });
        _slotRepo.GetAllAsync()
            .Returns(new List<Slot> { slot });

        var result = await _sut.GetExcludedSlotIdsAsync(_activityId, _orgId);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GivenUserConstraints_WhenUserIdProvided_ThenProcessesUserConstraintsToo()
    {
        var slotId = Guid.NewGuid();
        var userConstraint = new UserConstraint
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            UserId = _userId,
            SchedulingPeriodId = _periodId,
            WeekNum = null,
            Key = "user_custom",
            Value = "val"
        };

        _userConstraintRepo.GetByUserPeriodAsync(_userId, _periodId)
            .Returns(new List<UserConstraint> { userConstraint });

        var handler = Substitute.For<IConstraintHandler>();
        handler.ConstraintKey.Returns("user_custom");
        handler.ProcessConstraintAsync(Arg.Any<ActivityConstraint>(), _orgId)
            .Returns(new HashSet<Guid> { slotId });

        _sut = new ActivityConstraintProcessor(
            _constraintRepo, _userConstraintRepo, _slotRepo,
            new[] { handler },
            Substitute.For<ILogger<ActivityConstraintProcessor>>());

        var result = await _sut.GetExcludedSlotIdsAsync(
            _activityId, _orgId, _userId, _periodId);

        Assert.That(result, Does.Contain(slotId));
    }

    [Test]
    public async Task GivenWeekNumMismatch_WhenGetExcludedSlots_ThenSkipsConstraint()
    {
        var constraint = MakeActivityConstraint("custom", "val");
        constraint.WeekNum = 2;
        _constraintRepo.GetByActivityIdAsync(_activityId)
            .Returns(new List<ActivityConstraint> { constraint });

        var handler = Substitute.For<IConstraintHandler>();
        handler.ConstraintKey.Returns("custom");

        _sut = new ActivityConstraintProcessor(
            _constraintRepo, _userConstraintRepo, _slotRepo,
            new[] { handler },
            Substitute.For<ILogger<ActivityConstraintProcessor>>());

        var result = await _sut.GetExcludedSlotIdsAsync(
            _activityId, _orgId, weekNum: 1);

        Assert.That(result, Is.Empty);
        await handler.DidNotReceive().ProcessConstraintAsync(Arg.Any<ActivityConstraint>(), Arg.Any<Guid>());
    }

    [Test]
    public async Task GivenMultipleConstraints_WhenGetExcludedSlots_ThenUnionsAllExcludedSlots()
    {
        var slotId1 = Guid.NewGuid();
        var slotId2 = Guid.NewGuid();

        var c1 = MakeActivityConstraint("type_a", "v1");
        var c2 = MakeActivityConstraint("type_b", "v2");
        _constraintRepo.GetByActivityIdAsync(_activityId)
            .Returns(new List<ActivityConstraint> { c1, c2 });

        var handlerA = Substitute.For<IConstraintHandler>();
        handlerA.ConstraintKey.Returns("type_a");
        handlerA.ProcessConstraintAsync(c1, _orgId)
            .Returns(new HashSet<Guid> { slotId1 });

        var handlerB = Substitute.For<IConstraintHandler>();
        handlerB.ConstraintKey.Returns("type_b");
        handlerB.ProcessConstraintAsync(c2, _orgId)
            .Returns(new HashSet<Guid> { slotId2 });

        _sut = new ActivityConstraintProcessor(
            _constraintRepo, _userConstraintRepo, _slotRepo,
            new[] { handlerA, handlerB },
            Substitute.For<ILogger<ActivityConstraintProcessor>>());

        var result = await _sut.GetExcludedSlotIdsAsync(_activityId, _orgId);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result, Does.Contain(slotId1));
        Assert.That(result, Does.Contain(slotId2));
    }

    [Test]
    public async Task GivenUserConstraintsFromDifferentOrg_WhenGetExcludedSlots_ThenFiltersThemOut()
    {
        var foreignOrg = Guid.NewGuid();
        var userConstraint = new UserConstraint
        {
            Id = Guid.NewGuid(),
            OrganizationId = foreignOrg,
            UserId = _userId,
            SchedulingPeriodId = _periodId,
            WeekNum = null,
            Key = "forbidden_timerange",
            Value = "Monday 08:00 - 12:00"
        };

        _userConstraintRepo.GetByUserPeriodAsync(_userId, _periodId)
            .Returns(new List<UserConstraint> { userConstraint });

        var result = await _sut.GetExcludedSlotIdsAsync(
            _activityId, _orgId, _userId, _periodId);

        Assert.That(result, Is.Empty);
    }

    #region Helpers

    private ActivityConstraint MakeActivityConstraint(string key, string value)
    {
        return new ActivityConstraint
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            ActivityId = _activityId,
            WeekNum = null,
            Key = key,
            Value = value
        };
    }

    private Slot MakeSlot(string weekday, int fromHour, int toHour)
    {
        return new Slot
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SchedulingPeriodId = _periodId,
            Weekday = weekday,
            FromTime = TimeSpan.FromHours(fromHour),
            ToTime = TimeSpan.FromHours(toHour)
        };
    }

    #endregion
}
