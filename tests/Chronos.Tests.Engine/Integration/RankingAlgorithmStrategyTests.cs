using Chronos.Data.Context;
using Chronos.Data.Repositories.Resources;
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
public class RankingAlgorithmStrategyTests
{
    private AppDbContext _dbContext = null!;
    private IActivityRepository _activityRepository = null!;
    private ISlotRepository _slotRepository = null!;
    private IResourceRepository _resourceRepository = null!;
    private IAssignmentRepository _assignmentRepository = null!;
    private IConstraintProcessor _constraintProcessor = null!;
    private IConstraintEvaluator _constraintEvaluator = null!;
    private PreferenceWeightedRanker _ranker = null!;
    private IUserPreferenceRepository _preferenceRepository = null!;
    private RankingAlgorithmStrategy _sut = null!;

    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _periodId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options, null);

        _activityRepository = Substitute.For<IActivityRepository>();
        _slotRepository = Substitute.For<ISlotRepository>();
        _resourceRepository = Substitute.For<IResourceRepository>();
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

        _sut = new RankingAlgorithmStrategy(
            _activityRepository,
            _slotRepository,
            _resourceRepository,
            _assignmentRepository,
            _constraintProcessor,
            _constraintEvaluator,
            _ranker,
            _dbContext,
            Substitute.For<ILogger<RankingAlgorithmStrategy>>()
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
        var wrongRequest = new HandleConstraintChangeRequest(Guid.NewGuid(), _orgId, _periodId);

        Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ExecuteAsync(wrongRequest, CancellationToken.None));
    }

    [Test]
    public async Task GivenNoActivitiesForPeriod_WhenExecute_ThenReturnsSuccessZeroAssignments()
    {
        var subjectId = Guid.NewGuid();
        _dbContext.Subjects.Add(new Subject
        {
            Id = subjectId,
            OrganizationId = _orgId,
            DepartmentId = Guid.NewGuid(),
            SchedulingPeriodId = Guid.NewGuid(), // different period
            Code = "CS100",
            Name = "Intro",
        });
        _dbContext.SchedulingPeriods.Add(new SchedulingPeriod
        {
            Id = _periodId,
            OrganizationId = _orgId,
            Name = "Semester 1",
            FromDate = new DateTime(2026, 3, 2),
            ToDate = new DateTime(2026, 3, 8),
        });
        await _dbContext.SaveChangesAsync();

        _slotRepository.GetBySchedulingPeriodIdAsync(_periodId).Returns(new List<Slot>());
        _resourceRepository.GetAllAsync().Returns(new List<Resource>());

        var request = new SchedulePeriodRequest(_periodId, _orgId);
        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.AssignmentsCreated, Is.EqualTo(0));
        Assert.That(result.FailureReason, Is.EqualTo("No activities to schedule"));
    }

    [Test]
    public async Task GivenSchedulingPeriodNotFound_WhenExecute_ThenReturnsFailure()
    {
        var subjectId = Guid.NewGuid();
        _dbContext.Subjects.Add(new Subject
        {
            Id = subjectId,
            OrganizationId = _orgId,
            DepartmentId = Guid.NewGuid(),
            SchedulingPeriodId = _periodId,
            Code = "CS101",
            Name = "Algorithms",
        });
        _dbContext.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SubjectId = subjectId,
            AssignedUserId = Guid.NewGuid(),
            ActivityType = "Lecture",
            Duration = 60,
        });
        await _dbContext.SaveChangesAsync();

        _slotRepository.GetBySchedulingPeriodIdAsync(_periodId).Returns(new List<Slot>());
        _resourceRepository.GetAllAsync().Returns(new List<Resource>());

        var request = new SchedulePeriodRequest(_periodId, _orgId);
        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.FailureReason, Does.Contain("was not found"));
    }

    [Test]
    public async Task GivenSingleActivityAndMatchingSlot_WhenExecute_ThenCreatesAssignment()
    {
        var subjectId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _dbContext.SchedulingPeriods.Add(new SchedulingPeriod
        {
            Id = _periodId,
            OrganizationId = _orgId,
            Name = "Semester 1",
            FromDate = new DateTime(2026, 3, 2), // Monday
            ToDate = new DateTime(2026, 3, 8),   // Sunday — ISO week 10
        });
        _dbContext.Subjects.Add(new Subject
        {
            Id = subjectId,
            OrganizationId = _orgId,
            DepartmentId = Guid.NewGuid(),
            SchedulingPeriodId = _periodId,
            Code = "CS201",
            Name = "Data Structures",
        });
        _dbContext.Activities.Add(new Activity
        {
            Id = activityId,
            OrganizationId = _orgId,
            SubjectId = subjectId,
            AssignedUserId = userId,
            ActivityType = "Lecture",
            Duration = 60,
        });
        await _dbContext.SaveChangesAsync();

        var slot = new Slot
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SchedulingPeriodId = _periodId,
            Weekday = "Monday",
            FromTime = new TimeSpan(9, 0, 0),
            ToTime = new TimeSpan(10, 0, 0),
        };
        var resource = new Resource
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            ResourceTypeId = Guid.NewGuid(),
            Location = "Building A",
            Identifier = "Room 101",
        };

        _slotRepository.GetBySchedulingPeriodIdAsync(_periodId).Returns(new List<Slot> { slot });
        _resourceRepository.GetAllAsync().Returns(new List<Resource> { resource });
        _constraintProcessor.GetExcludedSlotIdsAsync(default, default, default, default, default)
            .ReturnsForAnyArgs(new HashSet<Guid>());
        _constraintEvaluator.CanAssignAsync(null!, null!, null!, default)
            .ReturnsForAnyArgs(true);

        var request = new SchedulePeriodRequest(_periodId, _orgId);
        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.That(result.Success, Is.True, result.FailureReason);
        Assert.That(result.AssignmentsCreated, Is.GreaterThanOrEqualTo(1));
        Assert.That(result.UnscheduledActivityIds, Is.Empty);
    }

    [Test]
    public async Task GivenActivityWithInvalidDuration_WhenExecute_ThenSkipsActivity()
    {
        var subjectId = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        _dbContext.SchedulingPeriods.Add(new SchedulingPeriod
        {
            Id = _periodId,
            OrganizationId = _orgId,
            Name = "Semester 1",
            FromDate = new DateTime(2026, 3, 2),
            ToDate = new DateTime(2026, 3, 8),
        });
        _dbContext.Subjects.Add(new Subject
        {
            Id = subjectId,
            OrganizationId = _orgId,
            DepartmentId = Guid.NewGuid(),
            SchedulingPeriodId = _periodId,
            Code = "CS300",
            Name = "Zero Duration",
        });
        _dbContext.Activities.Add(new Activity
        {
            Id = activityId,
            OrganizationId = _orgId,
            SubjectId = subjectId,
            AssignedUserId = Guid.NewGuid(),
            ActivityType = "Lab",
            Duration = 0,
        });
        await _dbContext.SaveChangesAsync();

        var slot = new Slot
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SchedulingPeriodId = _periodId,
            Weekday = "Monday",
            FromTime = new TimeSpan(9, 0, 0),
            ToTime = new TimeSpan(10, 0, 0),
        };

        _slotRepository.GetBySchedulingPeriodIdAsync(_periodId).Returns(new List<Slot> { slot });
        _resourceRepository.GetAllAsync().Returns(new List<Resource>
        {
            new() { Id = Guid.NewGuid(), OrganizationId = _orgId, ResourceTypeId = Guid.NewGuid(), Location = "A", Identifier = "R1" },
        });
        _constraintProcessor.GetExcludedSlotIdsAsync(default, default, default, default, default)
            .ReturnsForAnyArgs(new HashSet<Guid>());

        var request = new SchedulePeriodRequest(_periodId, _orgId);
        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.UnscheduledActivityIds, Contains.Item(activityId));
    }

    [Test]
    public async Task GivenAllSlotsExcluded_WhenExecute_ThenActivityIsUnscheduled()
    {
        var subjectId = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        var slot = new Slot
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SchedulingPeriodId = _periodId,
            Weekday = "Monday",
            FromTime = new TimeSpan(9, 0, 0),
            ToTime = new TimeSpan(10, 0, 0),
        };

        _dbContext.SchedulingPeriods.Add(new SchedulingPeriod
        {
            Id = _periodId,
            OrganizationId = _orgId,
            Name = "Semester 1",
            FromDate = new DateTime(2026, 3, 2),
            ToDate = new DateTime(2026, 3, 8),
        });
        _dbContext.Subjects.Add(new Subject
        {
            Id = subjectId,
            OrganizationId = _orgId,
            DepartmentId = Guid.NewGuid(),
            SchedulingPeriodId = _periodId,
            Code = "CS400",
            Name = "Blocked Course",
        });
        _dbContext.Activities.Add(new Activity
        {
            Id = activityId,
            OrganizationId = _orgId,
            SubjectId = subjectId,
            AssignedUserId = Guid.NewGuid(),
            ActivityType = "Lecture",
            Duration = 60,
        });
        await _dbContext.SaveChangesAsync();

        _slotRepository.GetBySchedulingPeriodIdAsync(_periodId).Returns(new List<Slot> { slot });
        _resourceRepository.GetAllAsync().Returns(new List<Resource>
        {
            new() { Id = Guid.NewGuid(), OrganizationId = _orgId, ResourceTypeId = Guid.NewGuid(), Location = "A", Identifier = "R1" },
        });
        _constraintProcessor.GetExcludedSlotIdsAsync(default, default, default, default, default)
            .ReturnsForAnyArgs(new HashSet<Guid> { slot.Id });
        _constraintEvaluator.CanAssignAsync(null!, null!, null!, default)
            .ReturnsForAnyArgs(true);

        var request = new SchedulePeriodRequest(_periodId, _orgId);
        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.That(result.UnscheduledActivityIds, Contains.Item(activityId));
    }

    [Test]
    public async Task GivenMultiSlotActivity_WhenConsecutiveSlotsAvailable_ThenCreatesMultipleAssignments()
    {
        var subjectId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        _dbContext.SchedulingPeriods.Add(new SchedulingPeriod
        {
            Id = _periodId,
            OrganizationId = _orgId,
            Name = "Semester 1",
            FromDate = new DateTime(2026, 3, 2),
            ToDate = new DateTime(2026, 3, 8),
        });
        _dbContext.Subjects.Add(new Subject
        {
            Id = subjectId,
            OrganizationId = _orgId,
            DepartmentId = Guid.NewGuid(),
            SchedulingPeriodId = _periodId,
            Code = "CS500",
            Name = "Double Lecture",
        });
        _dbContext.Activities.Add(new Activity
        {
            Id = activityId,
            OrganizationId = _orgId,
            SubjectId = subjectId,
            AssignedUserId = userId,
            ActivityType = "Lecture",
            Duration = 120, // 2 hours = 2 consecutive 60-min slots
        });
        await _dbContext.SaveChangesAsync();

        var slot1 = new Slot
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SchedulingPeriodId = _periodId,
            Weekday = "Monday",
            FromTime = new TimeSpan(9, 0, 0),
            ToTime = new TimeSpan(10, 0, 0),
        };
        var slot2 = new Slot
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SchedulingPeriodId = _periodId,
            Weekday = "Monday",
            FromTime = new TimeSpan(10, 0, 0),
            ToTime = new TimeSpan(11, 0, 0),
        };
        var resource = new Resource
        {
            Id = resourceId,
            OrganizationId = _orgId,
            ResourceTypeId = Guid.NewGuid(),
            Location = "Building A",
            Identifier = "Room 201",
        };

        _slotRepository.GetBySchedulingPeriodIdAsync(_periodId)
            .Returns(new List<Slot> { slot1, slot2 });
        _resourceRepository.GetAllAsync()
            .Returns(new List<Resource> { resource });
        _constraintProcessor.GetExcludedSlotIdsAsync(default, default, default, default, default)
            .ReturnsForAnyArgs(new HashSet<Guid>());
        _constraintEvaluator.CanAssignAsync(null!, null!, null!, default)
            .ReturnsForAnyArgs(true);

        var request = new SchedulePeriodRequest(_periodId, _orgId);
        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.That(result.Success, Is.True, result.FailureReason);
        Assert.That(result.AssignmentsCreated, Is.EqualTo(2));
        Assert.That(result.UnscheduledActivityIds, Is.Empty);
    }

    [Test]
    public async Task GivenInternalException_WhenExecute_ThenReturnsCaughtFailure()
    {
        var subjectId = Guid.NewGuid();
        _dbContext.Subjects.Add(new Subject
        {
            Id = subjectId,
            OrganizationId = _orgId,
            DepartmentId = Guid.NewGuid(),
            SchedulingPeriodId = _periodId,
            Code = "CS600",
            Name = "Error Course",
        });
        _dbContext.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SubjectId = subjectId,
            AssignedUserId = Guid.NewGuid(),
            ActivityType = "Lab",
            Duration = 60,
        });
        _dbContext.SchedulingPeriods.Add(new SchedulingPeriod
        {
            Id = _periodId,
            OrganizationId = _orgId,
            Name = "Semester 1",
            FromDate = new DateTime(2026, 3, 2),
            ToDate = new DateTime(2026, 3, 8),
        });
        await _dbContext.SaveChangesAsync();

        _slotRepository.GetBySchedulingPeriodIdAsync(_periodId)
            .Returns<List<Slot>>(_ => throw new InvalidOperationException("Storage failure"));

        var request = new SchedulePeriodRequest(_periodId, _orgId);
        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.FailureReason, Does.Contain("Algorithm failed"));
        Assert.That(result.FailureReason, Does.Contain("Storage failure"));
    }

    [Test]
    public async Task GivenDeleteByPeriodCalled_WhenExecute_ThenClearsExistingAssignments()
    {
        var subjectId = Guid.NewGuid();
        _dbContext.SchedulingPeriods.Add(new SchedulingPeriod
        {
            Id = _periodId,
            OrganizationId = _orgId,
            Name = "Semester 1",
            FromDate = new DateTime(2026, 3, 2),
            ToDate = new DateTime(2026, 3, 8),
        });
        _dbContext.Subjects.Add(new Subject
        {
            Id = subjectId,
            OrganizationId = _orgId,
            DepartmentId = Guid.NewGuid(),
            SchedulingPeriodId = _periodId,
            Code = "CS700",
            Name = "Cleanup",
        });
        _dbContext.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SubjectId = subjectId,
            AssignedUserId = Guid.NewGuid(),
            ActivityType = "Lecture",
            Duration = 60,
        });
        await _dbContext.SaveChangesAsync();

        _slotRepository.GetBySchedulingPeriodIdAsync(_periodId).Returns(new List<Slot>());
        _resourceRepository.GetAllAsync().Returns(new List<Resource>());

        var request = new SchedulePeriodRequest(_periodId, _orgId);
        await _sut.ExecuteAsync(request, CancellationToken.None);

        await _assignmentRepository.Received(1).DeleteBySchedulingPeriodIdAsync(_periodId);
    }

    [Test]
    public async Task GivenConstraintEvaluatorRejects_WhenExecute_ThenActivityIsUnscheduled()
    {
        var subjectId = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        _dbContext.SchedulingPeriods.Add(new SchedulingPeriod
        {
            Id = _periodId,
            OrganizationId = _orgId,
            Name = "Semester 1",
            FromDate = new DateTime(2026, 3, 2),
            ToDate = new DateTime(2026, 3, 8),
        });
        _dbContext.Subjects.Add(new Subject
        {
            Id = subjectId,
            OrganizationId = _orgId,
            DepartmentId = Guid.NewGuid(),
            SchedulingPeriodId = _periodId,
            Code = "CS800",
            Name = "Rejected",
        });
        _dbContext.Activities.Add(new Activity
        {
            Id = activityId,
            OrganizationId = _orgId,
            SubjectId = subjectId,
            AssignedUserId = Guid.NewGuid(),
            ActivityType = "Lecture",
            Duration = 60,
        });
        await _dbContext.SaveChangesAsync();

        var slot = new Slot
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SchedulingPeriodId = _periodId,
            Weekday = "Monday",
            FromTime = new TimeSpan(9, 0, 0),
            ToTime = new TimeSpan(10, 0, 0),
        };

        _slotRepository.GetBySchedulingPeriodIdAsync(_periodId).Returns(new List<Slot> { slot });
        _resourceRepository.GetAllAsync().Returns(new List<Resource>
        {
            new() { Id = Guid.NewGuid(), OrganizationId = _orgId, ResourceTypeId = Guid.NewGuid(), Location = "A", Identifier = "R1" },
        });
        _constraintProcessor.GetExcludedSlotIdsAsync(default, default, default, default, default)
            .ReturnsForAnyArgs(new HashSet<Guid>());
        _constraintEvaluator.CanAssignAsync(null!, null!, null!, default)
            .ReturnsForAnyArgs(false);

        var request = new SchedulePeriodRequest(_periodId, _orgId);
        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.That(result.UnscheduledActivityIds, Contains.Item(activityId));
        await _assignmentRepository.DidNotReceive().AddAsync(Arg.Any<Assignment>());
    }
}
