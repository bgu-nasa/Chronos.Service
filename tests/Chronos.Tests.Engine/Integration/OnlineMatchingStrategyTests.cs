using Chronos.Data.Context;
using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Resources;
using Chronos.Domain.Schedule;
using Chronos.Domain.Schedule.Messages;
using Chronos.Engine.Constraints;
using Chronos.Engine.Constraints.Evaluation;
using Chronos.Engine.Matching;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Chronos.Tests.Engine.Integration;

[TestFixture]
[Category("Integration")]
public class OnlineMatchingStrategyTests
{
    private AppDbContext _dbContext = null!;
    private IAssignmentRepository _assignmentRepository = null!;
    private IConstraintProcessor _constraintProcessor = null!;
    private IConstraintEvaluator _constraintEvaluator = null!;
    private PreferenceWeightedRanker _ranker = null!;
    private IUserPreferenceRepository _preferenceRepository = null!;
    private OnlineMatchingStrategy _sut = null!;

    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _periodId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options, null);

        _assignmentRepository = Substitute.For<IAssignmentRepository>();
        _constraintProcessor = Substitute.For<IConstraintProcessor>();
        _constraintEvaluator = Substitute.For<IConstraintEvaluator>();
        _preferenceRepository = Substitute.For<IUserPreferenceRepository>();
        _preferenceRepository.GetByUserIdAsync(Arg.Any<Guid>()).Returns(new List<UserPreference>());
        _preferenceRepository.GetByUserPeriodAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(new List<UserPreference>());

        _ranker = new PreferenceWeightedRanker(
            _preferenceRepository,
            Substitute.For<ILogger<PreferenceWeightedRanker>>()
        );

        _sut = new OnlineMatchingStrategy(
            _assignmentRepository,
            _constraintProcessor,
            _constraintEvaluator,
            _ranker,
            _dbContext,
            Substitute.For<ILogger<OnlineMatchingStrategy>>()
        );
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    [Test]
    public void GivenWrongRequestType_WhenExecute_ThenThrowsArgumentException()
    {
        var wrongRequest = new SchedulePeriodRequest(Guid.NewGuid(), _orgId);

        Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ExecuteAsync(wrongRequest, CancellationToken.None));
    }

    [Test]
    public async Task GivenActivityScopeWithNoActivityId_WhenConstraintNotFound_ThenReturnsFailure()
    {
        var constraintId = Guid.NewGuid();
        var request = new HandleConstraintChangeRequest(
            constraintId, _orgId, _periodId,
            ConstraintScope.Activity, ConstraintChangeOperation.Created,
            ActivityId: null
        );

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.FailureReason, Does.Contain("Activity id could not be resolved"));
    }

    [Test]
    public async Task GivenActivityScope_WhenActivityNotFoundInOrg_ThenReturnsFailure()
    {
        var activityId = Guid.NewGuid();
        var activity = new Activity
        {
            Id = activityId,
            OrganizationId = Guid.NewGuid(), // different org
            SubjectId = Guid.NewGuid(),
            AssignedUserId = Guid.NewGuid(),
            ActivityType = "Lecture",
            Duration = 60,
        };
        _dbContext.Activities.Add(activity);
        await _dbContext.SaveChangesAsync();

        var request = new HandleConstraintChangeRequest(
            Guid.NewGuid(), _orgId, _periodId,
            ConstraintScope.Activity, ConstraintChangeOperation.Updated,
            ActivityId: activityId
        );

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.FailureReason, Is.EqualTo("Activity not found"));
    }

    [Test]
    public async Task GivenActivityScope_WhenNoAssignments_ThenReturnsSuccessNotAssigned()
    {
        var activityId = Guid.NewGuid();
        _dbContext.Activities.Add(new Activity
        {
            Id = activityId,
            OrganizationId = _orgId,
            SubjectId = Guid.NewGuid(),
            AssignedUserId = Guid.NewGuid(),
            ActivityType = "Lab",
            Duration = 90,
        });
        await _dbContext.SaveChangesAsync();

        _assignmentRepository.GetByActivityIdAsync(activityId).Returns(new List<Assignment>());

        var request = new HandleConstraintChangeRequest(
            Guid.NewGuid(), _orgId, _periodId,
            ConstraintScope.Activity, ConstraintChangeOperation.Updated,
            ActivityId: activityId
        );

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.FailureReason, Is.EqualTo("Activity not currently assigned"));
    }

    [Test]
    public async Task GivenActivityScope_WhenAssignmentStillValid_ThenReturnsRemainsValid()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        var activity = new Activity
        {
            Id = activityId,
            OrganizationId = _orgId,
            SubjectId = Guid.NewGuid(),
            AssignedUserId = userId,
            ActivityType = "Lecture",
            Duration = 60,
        };
        _dbContext.Activities.Add(activity);

        var slot = new Slot
        {
            Id = slotId,
            OrganizationId = _orgId,
            SchedulingPeriodId = _periodId,
            Weekday = "Monday",
            FromTime = new TimeSpan(9, 0, 0),
            ToTime = new TimeSpan(10, 0, 0),
        };
        _dbContext.Slots.Add(slot);

        var resource = new Resource
        {
            Id = resourceId,
            OrganizationId = _orgId,
            ResourceTypeId = Guid.NewGuid(),
            Location = "Building A",
            Identifier = "Room 101",
        };
        _dbContext.Resources.Add(resource);

        await _dbContext.SaveChangesAsync();

        var assignment = new Assignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SlotId = slotId,
            ResourceId = resourceId,
            ActivityId = activityId,
        };
        _assignmentRepository.GetByActivityIdAsync(activityId).Returns(new List<Assignment> { assignment });

        _constraintProcessor.GetExcludedSlotIdsAsync(default, default, default, default, default)
            .ReturnsForAnyArgs(new HashSet<Guid>());

        _constraintEvaluator.CanAssignAsync(null!, null!, null!, default)
            .ReturnsForAnyArgs(true);

        var request = new HandleConstraintChangeRequest(
            Guid.NewGuid(), _orgId, _periodId,
            ConstraintScope.Activity, ConstraintChangeOperation.Updated,
            ActivityId: activityId
        );

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.FailureReason, Is.EqualTo("Current assignment remains valid"));
    }

    [Test]
    public async Task GivenActivityScope_WhenAssignmentInvalid_ThenReschedulesToSameSlot()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var resource2Id = Guid.NewGuid();

        var activity = new Activity
        {
            Id = activityId,
            OrganizationId = _orgId,
            SubjectId = Guid.NewGuid(),
            AssignedUserId = userId,
            ActivityType = "Lecture",
            Duration = 60,
        };
        _dbContext.Activities.Add(activity);

        var slot = new Slot
        {
            Id = slotId,
            OrganizationId = _orgId,
            SchedulingPeriodId = _periodId,
            Weekday = "Monday",
            FromTime = new TimeSpan(9, 0, 0),
            ToTime = new TimeSpan(10, 0, 0),
        };
        _dbContext.Slots.Add(slot);

        _dbContext.Resources.Add(new Resource
        {
            Id = resourceId,
            OrganizationId = _orgId,
            ResourceTypeId = Guid.NewGuid(),
            Location = "A",
            Identifier = "R1",
        });
        _dbContext.Resources.Add(new Resource
        {
            Id = resource2Id,
            OrganizationId = _orgId,
            ResourceTypeId = Guid.NewGuid(),
            Location = "A",
            Identifier = "R2",
        });
        await _dbContext.SaveChangesAsync();

        var currentAssignment = new Assignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SlotId = slotId,
            ResourceId = resourceId,
            ActivityId = activityId,
        };
        _assignmentRepository.GetByActivityIdAsync(activityId)
            .Returns(new List<Assignment> { currentAssignment });
        _assignmentRepository.GetBySchedulingPeriodIdAsync(_periodId)
            .Returns(new List<Assignment> { currentAssignment });

        _constraintProcessor.GetExcludedSlotIdsAsync(default, default, default, default, default)
            .ReturnsForAnyArgs(new HashSet<Guid> { slotId });

        _constraintEvaluator.CanAssignAsync(null!, null!, null!, default)
            .ReturnsForAnyArgs(true);

        var request = new HandleConstraintChangeRequest(
            Guid.NewGuid(), _orgId, _periodId,
            ConstraintScope.Activity, ConstraintChangeOperation.Updated,
            ActivityId: activityId
        );

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        // Slot was excluded so same-slot stage fails, fallback also fails (only 1 slot and it's excluded)
        Assert.That(result.Success, Is.False);
        Assert.That(result.FailureReason, Does.Contain("could not be rescheduled"));
    }

    [Test]
    public async Task GivenUserScope_WhenMissingUserId_ThenReturnsFailure()
    {
        var request = new HandleConstraintChangeRequest(
            Guid.NewGuid(), _orgId, _periodId,
            ConstraintScope.User, ConstraintChangeOperation.Created,
            UserId: null
        );

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.FailureReason, Does.Contain("User-scope event missing user id"));
    }

    [Test]
    public async Task GivenUserScope_WhenMissingSchedulingPeriodId_ThenReturnsFailure()
    {
        var request = new HandleConstraintChangeRequest(
            Guid.NewGuid(), _orgId, Guid.Empty,
            ConstraintScope.User, ConstraintChangeOperation.Created,
            UserId: Guid.NewGuid()
        );

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.FailureReason, Does.Contain("User-scope event missing scheduling period id"));
    }

    [Test]
    public async Task GivenUserScope_WhenNoAssignments_ThenReturnsSuccessNoAssignments()
    {
        var userId = Guid.NewGuid();
        _assignmentRepository.GetBySchedulingPeriodIdAsync(_periodId)
            .Returns(new List<Assignment>());

        var request = new HandleConstraintChangeRequest(
            Guid.NewGuid(), _orgId, _periodId,
            ConstraintScope.User, ConstraintChangeOperation.Updated,
            UserId: userId
        );

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.FailureReason, Does.Contain("No assignments found"));
    }

    [Test]
    public async Task GivenUserScope_WhenNoAffectedActivities_ThenReturnsSuccess()
    {
        var userId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var slotId = Guid.NewGuid();

        _dbContext.Activities.Add(new Activity
        {
            Id = activityId,
            OrganizationId = _orgId,
            SubjectId = Guid.NewGuid(),
            AssignedUserId = differentUserId, // assigned to a different user
            ActivityType = "Seminar",
            Duration = 60,
        });
        await _dbContext.SaveChangesAsync();

        _assignmentRepository.GetBySchedulingPeriodIdAsync(_periodId)
            .Returns(new List<Assignment>
            {
                new() { Id = Guid.NewGuid(), OrganizationId = _orgId, SlotId = slotId, ResourceId = Guid.NewGuid(), ActivityId = activityId },
            });

        var request = new HandleConstraintChangeRequest(
            Guid.NewGuid(), _orgId, _periodId,
            ConstraintScope.User, ConstraintChangeOperation.Updated,
            UserId: userId
        );

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.FailureReason, Does.Contain("No assigned activities were affected"));
    }

    [Test]
    public async Task GivenInternalException_WhenExecute_ThenReturnsCaughtFailure()
    {
        var activityId = Guid.NewGuid();
        _dbContext.Activities.Add(new Activity
        {
            Id = activityId,
            OrganizationId = _orgId,
            SubjectId = Guid.NewGuid(),
            AssignedUserId = Guid.NewGuid(),
            ActivityType = "Lecture",
            Duration = 60,
        });
        await _dbContext.SaveChangesAsync();

        _assignmentRepository.GetByActivityIdAsync(activityId)
            .Returns<List<Assignment>>(_ => throw new InvalidOperationException("DB connection lost"));

        var request = new HandleConstraintChangeRequest(
            Guid.NewGuid(), _orgId, _periodId,
            ConstraintScope.Activity, ConstraintChangeOperation.Updated,
            ActivityId: activityId
        );

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.FailureReason, Does.Contain("Algorithm failed"));
        Assert.That(result.FailureReason, Does.Contain("DB connection lost"));
    }
}
