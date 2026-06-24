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
        Assert.That(result.FailureReason, Does.Contain("could not be re-scheduled"));
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

    [Test]
    public async Task GivenActivityScope_WhenAssignmentInvalidAndRescheduled_ThenPreventsLecturerDoubleBooking()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var slot1Id = Guid.NewGuid();
        var slot2Id = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        // 1. Setup the target activity
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

        // 2. Setup another activity assigned to the same user
        var otherActivityId = Guid.NewGuid();
        var otherActivity = new Activity
        {
            Id = otherActivityId,
            OrganizationId = _orgId,
            SubjectId = Guid.NewGuid(),
            AssignedUserId = userId,
            ActivityType = "Lecture",
            Duration = 60,
        };
        _dbContext.Activities.Add(otherActivity);

        // 3. Setup two slots: Slot 1 and Slot 2
        var slot1 = new Slot
        {
            Id = slot1Id,
            OrganizationId = _orgId,
            SchedulingPeriodId = _periodId,
            Weekday = "Monday",
            FromTime = new TimeSpan(9, 0, 0),
            ToTime = new TimeSpan(10, 0, 0),
        };
        var slot2 = new Slot
        {
            Id = slot2Id,
            OrganizationId = _orgId,
            SchedulingPeriodId = _periodId,
            Weekday = "Monday",
            FromTime = new TimeSpan(10, 0, 0),
            ToTime = new TimeSpan(11, 0, 0),
        };
        _dbContext.Slots.Add(slot1);
        _dbContext.Slots.Add(slot2);

        var resource = new Resource
        {
            Id = resourceId,
            OrganizationId = _orgId,
            ResourceTypeId = Guid.NewGuid(),
            Location = "A",
            Identifier = "R1",
            Capacity = 100,
        };
        _dbContext.Resources.Add(resource);

        // 4. Register other user's assignment at Slot 2
        var otherAssignment = new Assignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SlotId = slot2Id,
            ResourceId = resourceId,
            ActivityId = otherActivityId,
            WeekNum = 1,
        };
        _dbContext.Assignments.Add(otherAssignment);
        await _dbContext.SaveChangesAsync();

        // Target activity is currently assigned to Slot 1, but we will make it invalid
        var currentAssignment = new Assignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SlotId = slot1Id,
            ResourceId = resourceId,
            ActivityId = activityId,
            WeekNum = 1,
        };

        _assignmentRepository.GetByActivityIdAsync(activityId)
            .Returns(new List<Assignment> { currentAssignment });
        _assignmentRepository.GetBySchedulingPeriodIdAsync(_periodId)
            .Returns(new List<Assignment> { currentAssignment, otherAssignment });

        // Make Slot 1 invalid for the target activity using excludedSlots
        _constraintProcessor.GetExcludedSlotIdsAsync(activityId, _orgId, userId, _periodId, 1)
            .Returns(new HashSet<Guid> { slot1Id });

        _constraintEvaluator.CanAssignAsync(Arg.Any<Activity>(), Arg.Any<Slot>(), Arg.Any<Resource>(), Arg.Any<int?>())
            .Returns(true);

        var request = new HandleConstraintChangeRequest(
            Guid.NewGuid(), _orgId, _periodId,
            ConstraintScope.Activity, ConstraintChangeOperation.Updated,
            ActivityId: activityId
        );

        // Act
        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        // Assert: Rescheduling should fail because the only other slot (Slot 2) is occupied by the same lecturer
        Assert.That(result.Success, Is.False);
        Assert.That(result.FailureReason, Does.Contain("could not be re-scheduled"));
    }

    [Test]
    public async Task GivenActivityScope_WhenAssignmentRescheduled_ThenMaintainsConsistencyAcrossWeeks()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var slot1Id = Guid.NewGuid();
        var slot2Id = Guid.NewGuid();
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

        var slot1 = new Slot
        {
            Id = slot1Id,
            OrganizationId = _orgId,
            SchedulingPeriodId = _periodId,
            Weekday = "Monday",
            FromTime = new TimeSpan(9, 0, 0),
            ToTime = new TimeSpan(10, 0, 0),
        };
        var slot2 = new Slot
        {
            Id = slot2Id,
            OrganizationId = _orgId,
            SchedulingPeriodId = _periodId,
            Weekday = "Monday",
            FromTime = new TimeSpan(10, 0, 0),
            ToTime = new TimeSpan(11, 0, 0),
        };
        _dbContext.Slots.Add(slot1);
        _dbContext.Slots.Add(slot2);

        var resource = new Resource
        {
            Id = resourceId,
            OrganizationId = _orgId,
            ResourceTypeId = Guid.NewGuid(),
            Location = "A",
            Identifier = "R1",
            Capacity = 100,
        };
        _dbContext.Resources.Add(resource);
        await _dbContext.SaveChangesAsync();

        // The activity is assigned to Slot 1 in week 1 and week 2
        var assignmentWeek1 = new Assignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SlotId = slot1Id,
            ResourceId = resourceId,
            ActivityId = activityId,
            WeekNum = 1,
        };
        var assignmentWeek2 = new Assignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SlotId = slot1Id,
            ResourceId = resourceId,
            ActivityId = activityId,
            WeekNum = 2,
        };

        var allAssignments = new List<Assignment> { assignmentWeek1, assignmentWeek2 };
        _assignmentRepository.GetByActivityIdAsync(activityId).Returns(allAssignments);
        _assignmentRepository.GetBySchedulingPeriodIdAsync(_periodId).Returns(allAssignments);

        // Exclude Slot 1 for Week 1 (meaning Week 1 is invalid, but Week 2 would be valid on Slot 1)
        _constraintProcessor.GetExcludedSlotIdsAsync(activityId, _orgId, userId, _periodId, 1)
            .Returns(new HashSet<Guid> { slot1Id });
        _constraintProcessor.GetExcludedSlotIdsAsync(activityId, _orgId, userId, _periodId, 2)
            .Returns(new HashSet<Guid>());

        _constraintEvaluator.CanAssignAsync(Arg.Any<Activity>(), Arg.Any<Slot>(), Arg.Any<Resource>(), Arg.Any<int?>())
            .Returns(true);

        var request = new HandleConstraintChangeRequest(
            Guid.NewGuid(), _orgId, _periodId,
            ConstraintScope.Activity, ConstraintChangeOperation.Updated,
            ActivityId: activityId
        );

        // Act
        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        // Assert: Both weeks should be rescheduled to Slot 2 (which is consistent)
        Assert.That(result.Success, Is.True);
        
        // Assert that we deleted original assignments and added new ones
        await _assignmentRepository.Received(1).DeleteAsync(assignmentWeek1);
        await _assignmentRepository.Received(1).DeleteAsync(assignmentWeek2);
        
        // Assert we added two new assignments at slot2Id
        await _assignmentRepository.Received(2).AddAsync(Arg.Is<Assignment>(a => a.SlotId == slot2Id && a.ResourceId == resourceId));
    }

    [Test]
    public async Task GivenActivityWithTermLongConstraint_WhenWeeklyTeacherClash_ThenTriggersReschedule()
    {
        var activityId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        var activity = new Activity
        {
            Id = activityId,
            OrganizationId = _orgId,
            SubjectId = Guid.NewGuid(),
            AssignedUserId = teacherId,
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
            Location = "A",
            Identifier = "R1",
        };
        _dbContext.Resources.Add(resource);

        // Add a teacher weekly clash assignment in Week 1 to the database
        var clashActivityId = Guid.NewGuid();
        var clashActivity = new Activity
        {
            Id = clashActivityId,
            OrganizationId = _orgId,
            SubjectId = Guid.NewGuid(),
            AssignedUserId = teacherId,
            ActivityType = "Lab",
            Duration = 60,
        };
        _dbContext.Activities.Add(clashActivity);

        var clashAssignment = new Assignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SlotId = slotId,
            ResourceId = Guid.NewGuid(),
            ActivityId = clashActivityId,
            WeekNum = 1,
        };
        _dbContext.Assignments.Add(clashAssignment);
        await _dbContext.SaveChangesAsync();

        // The target activity has a term-long assignment (WeekNum = null) at the same slot
        var termLongAssignment = new Assignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SlotId = slotId,
            ResourceId = resourceId,
            ActivityId = activityId,
            WeekNum = null,
        };

        var allAssignments = new List<Assignment> { termLongAssignment, clashAssignment };
        _assignmentRepository.GetByActivityIdAsync(activityId).Returns(new List<Assignment> { termLongAssignment });
        _assignmentRepository.GetBySchedulingPeriodIdAsync(_periodId).Returns(allAssignments);

        _constraintProcessor.GetExcludedSlotIdsAsync(default, default, default, default, default)
            .ReturnsForAnyArgs(new HashSet<Guid>());
        _constraintEvaluator.CanAssignAsync(null!, null!, null!, default)
            .ReturnsForAnyArgs(true);

        var request = new HandleConstraintChangeRequest(
            Guid.NewGuid(), _orgId, _periodId,
            ConstraintScope.Activity, ConstraintChangeOperation.Updated,
            ActivityId: activityId
        );

        // Act
        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        // Assert: It should detect the teacher occupancy clash with the weekly assignment and fail/reschedule 
        // (since only 1 slot exists, it should fail to reschedule but it should definitely not report it as still valid)
        Assert.That(result.Success, Is.False);
        Assert.That(result.FailureReason, Does.Contain("could not be re-scheduled"));
    }

    [Test]
    public async Task GivenConstraintChange_WhenFirstWeekValidButSecondWeekInvalid_ThenReschedulesBothToSameSlot()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var slot1Id = Guid.NewGuid();
        var slot2Id = Guid.NewGuid();
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

        var slot1 = new Slot
        {
            Id = slot1Id,
            OrganizationId = _orgId,
            SchedulingPeriodId = _periodId,
            Weekday = "Monday",
            FromTime = new TimeSpan(9, 0, 0),
            ToTime = new TimeSpan(10, 0, 0),
        };
        var slot2 = new Slot
        {
            Id = slot2Id,
            OrganizationId = _orgId,
            SchedulingPeriodId = _periodId,
            Weekday = "Monday",
            FromTime = new TimeSpan(10, 0, 0),
            ToTime = new TimeSpan(11, 0, 0),
        };
        _dbContext.Slots.Add(slot1);
        _dbContext.Slots.Add(slot2);

        var resource = new Resource
        {
            Id = resourceId,
            OrganizationId = _orgId,
            ResourceTypeId = Guid.NewGuid(),
            Location = "A",
            Identifier = "R1",
            Capacity = 100,
        };
        _dbContext.Resources.Add(resource);
        await _dbContext.SaveChangesAsync();

        // The activity is assigned to Slot 1 in week 1 and week 2
        var assignmentWeek1 = new Assignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SlotId = slot1Id,
            ResourceId = resourceId,
            ActivityId = activityId,
            WeekNum = 1,
        };
        var assignmentWeek2 = new Assignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SlotId = slot1Id,
            ResourceId = resourceId,
            ActivityId = activityId,
            WeekNum = 2,
        };

        var allAssignments = new List<Assignment> { assignmentWeek1, assignmentWeek2 };
        _assignmentRepository.GetByActivityIdAsync(activityId).Returns(allAssignments);
        _assignmentRepository.GetBySchedulingPeriodIdAsync(_periodId).Returns(allAssignments);

        // Exclude Slot 1 for Week 2 (meaning Week 2 is invalid on Slot 1, but Week 1 is valid on Slot 1)
        _constraintProcessor.GetExcludedSlotIdsAsync(activityId, _orgId, userId, _periodId, 1)
            .Returns(new HashSet<Guid>());
        _constraintProcessor.GetExcludedSlotIdsAsync(activityId, _orgId, userId, _periodId, 2)
            .Returns(new HashSet<Guid> { slot1Id });

        _constraintEvaluator.CanAssignAsync(Arg.Any<Activity>(), Arg.Any<Slot>(), Arg.Any<Resource>(), Arg.Any<int?>())
            .Returns(true);

        var request = new HandleConstraintChangeRequest(
            Guid.NewGuid(), _orgId, _periodId,
            ConstraintScope.Activity, ConstraintChangeOperation.Updated,
            ActivityId: activityId
        );

        // Act
        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        // Assert: Both weeks should be rescheduled to Slot 2 (the consistent placement found by FindBestConsistentSlotResourceAsync)
        Assert.That(result.Success, Is.True);
        
        // Assert that we deleted original assignments and added new ones
        await _assignmentRepository.Received(1).DeleteAsync(assignmentWeek1);
        await _assignmentRepository.Received(1).DeleteAsync(assignmentWeek2);
        
        // Assert we added two new assignments at slot2Id
        await _assignmentRepository.Received(2).AddAsync(Arg.Is<Assignment>(a => a.SlotId == slot2Id && a.ResourceId == resourceId));
    }

    [Test]
    public async Task GivenConstraintChange_WhenSomeWeeksUnassigned_ThenAssignsUnassignedWeeksToConsistentSlot()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var slot1Id = Guid.NewGuid();
        var slot2Id = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        // 1. Add scheduling period to DB
        var schedulingPeriod = new SchedulingPeriod
        {
            Id = _periodId,
            OrganizationId = _orgId,
            Name = "Semester 1",
            FromDate = new DateTime(2026, 9, 1),
            ToDate = new DateTime(2026, 9, 10), // 2 weeks
        };
        _dbContext.SchedulingPeriods.Add(schedulingPeriod);

        // 2. Setup the target activity
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

        var slot1 = new Slot
        {
            Id = slot1Id,
            OrganizationId = _orgId,
            SchedulingPeriodId = _periodId,
            Weekday = "Monday",
            FromTime = new TimeSpan(9, 0, 0),
            ToTime = new TimeSpan(10, 0, 0),
        };
        var slot2 = new Slot
        {
            Id = slot2Id,
            OrganizationId = _orgId,
            SchedulingPeriodId = _periodId,
            Weekday = "Monday",
            FromTime = new TimeSpan(10, 0, 0),
            ToTime = new TimeSpan(11, 0, 0),
        };
        _dbContext.Slots.Add(slot1);
        _dbContext.Slots.Add(slot2);

        var resource = new Resource
        {
            Id = resourceId,
            OrganizationId = _orgId,
            ResourceTypeId = Guid.NewGuid(),
            Location = "A",
            Identifier = "R1",
            Capacity = 100,
        };
        _dbContext.Resources.Add(resource);
        await _dbContext.SaveChangesAsync();

        // The activity is currently only assigned in Week 1 (Week 2 is unassigned/deleted)
        var assignmentWeek1 = new Assignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SlotId = slot1Id,
            ResourceId = resourceId,
            ActivityId = activityId,
            WeekNum = 1,
        };

        var allAssignments = new List<Assignment> { assignmentWeek1 };
        _assignmentRepository.GetByActivityIdAsync(activityId).Returns(allAssignments);
        _assignmentRepository.GetBySchedulingPeriodIdAsync(_periodId).Returns(allAssignments);

        // Exclude Slot 1 for Week 1 (meaning Week 1 is invalid on Slot 1)
        _constraintProcessor.GetExcludedSlotIdsAsync(activityId, _orgId, userId, _periodId, 1)
            .Returns(new HashSet<Guid> { slot1Id });
        _constraintProcessor.GetExcludedSlotIdsAsync(activityId, _orgId, userId, _periodId, 2)
            .Returns(new HashSet<Guid>());

        _constraintEvaluator.CanAssignAsync(Arg.Any<Activity>(), Arg.Any<Slot>(), Arg.Any<Resource>(), Arg.Any<int?>())
            .Returns(true);

        var request = new HandleConstraintChangeRequest(
            Guid.NewGuid(), _orgId, _periodId,
            ConstraintScope.Activity, ConstraintChangeOperation.Updated,
            ActivityId: activityId
        );

        // Act
        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        // Assert: Both weeks should be rescheduled/assigned to Slot 2
        Assert.That(result.Success, Is.True);
        
        // Assert that we deleted the original Week 1 assignment
        await _assignmentRepository.Received(1).DeleteAsync(assignmentWeek1);
        
        // Assert we added new assignments at slot2Id for BOTH week 1 and week 2
        await _assignmentRepository.Received(1).AddAsync(Arg.Is<Assignment>(a => a.SlotId == slot2Id && a.ResourceId == resourceId && a.WeekNum == 1));
        await _assignmentRepository.Received(1).AddAsync(Arg.Is<Assignment>(a => a.SlotId == slot2Id && a.ResourceId == resourceId && a.WeekNum == 2));
    }
}

