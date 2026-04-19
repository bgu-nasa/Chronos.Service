using Chronos.Data.Repositories.Schedule;
using Chronos.Data.Repositories.Resources;
using Chronos.Domain.Schedule;
using Chronos.Domain.Resources;
using Chronos.MainApi.Schedule.Messaging;
using Chronos.MainApi.Schedule.Services;
using Chronos.MainApi.Shared.ExternalMangement;
using Chronos.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Chronos.Tests.MainApi.Services.Schedule;

[TestFixture]
[Category("Unit")]
public class ActivityConstraintServiceTests
{
    private IActivityConstraintRepository _activityConstraintRepository = null!;
    private IActivityRepository _activityRepository = null!;
    private ISubjectRepository _subjectRepository = null!;
    private IManagementExternalService _validationService = null!;
    private IMessagePublisher _messagePublisher = null!;
    private ILogger<ActivityConstraintService> _logger = null!;
    private ActivityConstraintService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _activityConstraintRepository = Substitute.For<IActivityConstraintRepository>();
        _activityRepository = Substitute.For<IActivityRepository>();
        _subjectRepository = Substitute.For<ISubjectRepository>();
        _validationService = Substitute.For<IManagementExternalService>();
        _messagePublisher = Substitute.For<IMessagePublisher>();
        _logger = Substitute.For<ILogger<ActivityConstraintService>>();

        _service = new ActivityConstraintService(
            _activityConstraintRepository,
            _logger,
            _validationService,
            _messagePublisher,
            _activityRepository,
            _subjectRepository);
    }

    #region CreateActivityConstraintAsync Tests

    [Test]
    public async Task CreateActivityConstraintAsync_WithValidData_ReturnsNewId()
    {
        var organizationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var key = "max_students";
        var value = """{"limit": 30}""";

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _activityRepository.GetByIdAsync(activityId).Returns(new Activity
        {
            Id = activityId,
            OrganizationId = organizationId,
            SubjectId = Guid.NewGuid(),
            AssignedUserId = Guid.Empty,
            ActivityType = "Lecture"
        });
        _subjectRepository.GetByIdAsync(Arg.Any<Guid>()).Returns(new Subject
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            DepartmentId = Guid.NewGuid(),
            SchedulingPeriodId = Guid.NewGuid(),
            Code = "MATH101",
            Name = "Math"
        });

        var result = await _service.CreateActivityConstraintAsync(organizationId, activityId, key, value);

        Assert.That(result, Is.Not.EqualTo(Guid.Empty));
        await _activityConstraintRepository.Received(1).AddAsync(Arg.Is<ActivityConstraint>(c =>
            c.OrganizationId == organizationId &&
            c.ActivityId == activityId &&
            c.Key == key &&
            c.Value == value));
        await _messagePublisher.Received(1).PublishAsync(Arg.Any<Chronos.Domain.Schedule.Messages.HandleConstraintChangeRequest>(), "request.online");
    }

    [Test]
    public void CreateActivityConstraintAsync_WithInvalidJson_ThrowsArgumentException()
    {
        var organizationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var key = "required_capacity"; // JSON validation only runs for required_capacity, time_range
        var value = "not valid json";

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);

        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.CreateActivityConstraintAsync(organizationId, activityId, key, value));

        Assert.That(ex!.Message, Does.Contain("valid JSON"));
    }

    [Test]
    public void CreateActivityConstraintAsync_WithEmptyValue_ThrowsArgumentException()
    {
        var organizationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var key = "max_students";
        var value = "";

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);

        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.CreateActivityConstraintAsync(organizationId, activityId, key, value));

        Assert.That(ex!.Message, Does.Contain("null or empty"));
    }

    [Test]
    public void CreateActivityConstraintAsync_WithWhitespaceValue_ThrowsArgumentException()
    {
        var organizationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var key = "max_students";
        var value = "   ";

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);

        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.CreateActivityConstraintAsync(organizationId, activityId, key, value));

        Assert.That(ex!.Message, Does.Contain("null or empty"));
    }

    [Test]
    public async Task CreateActivityConstraintAsync_WithComplexJson_Succeeds()
    {
        var organizationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var key = "schedule_preferences";
        var value = """{"days": ["Monday", "Wednesday"], "times": {"start": "09:00", "end": "17:00"}}""";

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _activityRepository.GetByIdAsync(activityId).Returns(new Activity
        {
            Id = activityId,
            OrganizationId = organizationId,
            SubjectId = Guid.NewGuid(),
            AssignedUserId = Guid.Empty,
            ActivityType = "Lecture"
        });
        _subjectRepository.GetByIdAsync(Arg.Any<Guid>()).Returns(new Subject
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            DepartmentId = Guid.NewGuid(),
            SchedulingPeriodId = Guid.NewGuid(),
            Code = "MATH101",
            Name = "Math"
        });

        var result = await _service.CreateActivityConstraintAsync(organizationId, activityId, key, value);

        Assert.That(result, Is.Not.EqualTo(Guid.Empty));
    }

    #endregion

    #region GetActivityConstraintByIdAsync Tests

    [Test]
    public async Task GetActivityConstraintByIdAsync_WithExistingConstraint_ReturnsConstraint()
    {
        var organizationId = Guid.NewGuid();
        var constraintId = Guid.NewGuid();
        var constraint = new ActivityConstraint
        {
            Id = constraintId,
            OrganizationId = organizationId,
            ActivityId = Guid.NewGuid(),
            Key = "max_students",
            Value = """{"limit": 30}"""
        };

        _activityConstraintRepository.GetByIdAsync(constraintId).Returns(constraint);

        var result = await _service.GetActivityConstraintByIdAsync(organizationId, constraintId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(constraintId));
    }

    [Test]
    public void GetActivityConstraintByIdAsync_WithNonExistentConstraint_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var constraintId = Guid.NewGuid();

        _activityConstraintRepository.GetByIdAsync(constraintId).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.GetActivityConstraintByIdAsync(organizationId, constraintId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public void GetActivityConstraintByIdAsync_WithWrongOrganization_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var wrongOrgId = Guid.NewGuid();
        var constraintId = Guid.NewGuid();
        var constraint = new ActivityConstraint
        {
            Id = constraintId,
            OrganizationId = wrongOrgId,
            ActivityId = Guid.NewGuid(),
            Key = "max_students",
            Value = """{"limit": 30}"""
        };

        _activityConstraintRepository.GetByIdAsync(constraintId).Returns(constraint);

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.GetActivityConstraintByIdAsync(organizationId, constraintId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion

    #region GetAllActivityConstraintsAsync Tests

    [Test]
    public async Task GetAllActivityConstraintsAsync_ReturnsOnlyOrganizationConstraints()
    {
        var organizationId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var constraints = new List<ActivityConstraint>
        {
            new ActivityConstraint
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ActivityId = Guid.NewGuid(),
                Key = "constraint1",
                Value = """{"value": 1}"""
            },
            new ActivityConstraint
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ActivityId = Guid.NewGuid(),
                Key = "constraint2",
                Value = """{"value": 2}"""
            },
            new ActivityConstraint
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrgId,
                ActivityId = Guid.NewGuid(),
                Key = "constraint3",
                Value = """{"value": 3}"""
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _activityConstraintRepository.GetAllAsync().Returns(constraints);

        var result = await _service.GetAllActivityConstraintsAsync(organizationId);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.All(c => c.OrganizationId == organizationId), Is.True);
    }

    [Test]
    public async Task GetAllActivityConstraintsAsync_WithNoConstraints_ReturnsEmptyList()
    {
        var organizationId = Guid.NewGuid();

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _activityConstraintRepository.GetAllAsync().Returns(new List<ActivityConstraint>());

        var result = await _service.GetAllActivityConstraintsAsync(organizationId);

        Assert.That(result, Is.Empty);
    }

    #endregion

    #region GetByActivityIdAsync Tests

    [Test]
    public async Task GetByActivityIdAsync_ReturnsConstraintsForActivity()
    {
        var organizationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var constraints = new List<ActivityConstraint>
        {
            new ActivityConstraint
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ActivityId = activityId,
                Key = "constraint1",
                Value = """{"value": 1}"""
            },
            new ActivityConstraint
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ActivityId = activityId,
                Key = "constraint2",
                Value = """{"value": 2}"""
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _activityConstraintRepository.GetByActivityIdAsync(activityId).Returns(constraints);

        var result = await _service.GetByActivityIdAsync(organizationId, activityId);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetByActivityIdAsync_FiltersOutOtherOrganizationConstraints()
    {
        var organizationId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var constraints = new List<ActivityConstraint>
        {
            new ActivityConstraint
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ActivityId = activityId,
                Key = "constraint1",
                Value = """{"value": 1}"""
            },
            new ActivityConstraint
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrgId,
                ActivityId = activityId,
                Key = "constraint2",
                Value = """{"value": 2}"""
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _activityConstraintRepository.GetByActivityIdAsync(activityId).Returns(constraints);

        var result = await _service.GetByActivityIdAsync(organizationId, activityId);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].OrganizationId, Is.EqualTo(organizationId));
    }

    #endregion

    #region UpdateActivityConstraintAsync Tests

    [Test]
    public async Task UpdateActivityConstraintAsync_WithValidData_UpdatesConstraint()
    {
        var organizationId = Guid.NewGuid();
        var constraintId = Guid.NewGuid();
        var constraint = new ActivityConstraint
        {
            Id = constraintId,
            OrganizationId = organizationId,
            ActivityId = Guid.NewGuid(),
            Key = "old_key",
            Value = """{"old": true}"""
        };

        _activityConstraintRepository.GetByIdAsync(constraintId).Returns(constraint);

        var newKey = "new_key";
        var newValue = """{"new": true}""";

        var result = await _service.UpdateActivityConstraintAsync(organizationId, constraintId, newKey, newValue);

        Assert.That(result.Key, Is.EqualTo(newKey));
        Assert.That(result.Value, Is.EqualTo(newValue));
        await _activityConstraintRepository.Received(1).UpdateAsync(Arg.Is<ActivityConstraint>(c =>
            c.Key == newKey && c.Value == newValue));
    }

    [Test]
    public void UpdateActivityConstraintAsync_WithInvalidJson_ThrowsArgumentException()
    {
        var organizationId = Guid.NewGuid();
        var constraintId = Guid.NewGuid();
        var constraint = new ActivityConstraint
        {
            Id = constraintId,
            OrganizationId = organizationId,
            ActivityId = Guid.NewGuid(),
            Key = "required_capacity", // JSON validation only runs for required_capacity, time_range
            Value = """{"min": 30}"""
        };

        _activityConstraintRepository.GetByIdAsync(constraintId).Returns(constraint);

        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.UpdateActivityConstraintAsync(organizationId, constraintId, "required_capacity", "invalid json"));

        Assert.That(ex!.Message, Does.Contain("valid JSON"));
    }

    [Test]
    public void UpdateActivityConstraintAsync_WithNonExistentConstraint_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var constraintId = Guid.NewGuid();

        _activityConstraintRepository.GetByIdAsync(constraintId).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.UpdateActivityConstraintAsync(organizationId, constraintId, "key", """{"value": true}"""));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion

    #region DeleteActivityConstraintAsync Tests

    [Test]
    public async Task DeleteActivityConstraintAsync_WithExistingConstraint_DeletesConstraint()
    {
        var organizationId = Guid.NewGuid();
        var constraintId = Guid.NewGuid();
        var constraint = new ActivityConstraint
        {
            Id = constraintId,
            OrganizationId = organizationId,
            ActivityId = Guid.NewGuid(),
            Key = "key",
            Value = """{"value": true}"""
        };

        _activityConstraintRepository.GetByIdAsync(constraintId).Returns(constraint);

        await _service.DeleteActivityConstraintAsync(organizationId, constraintId);

        await _activityConstraintRepository.Received(1).DeleteAsync(constraint);
    }

    [Test]
    public void DeleteActivityConstraintAsync_WithNonExistentConstraint_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var constraintId = Guid.NewGuid();

        _activityConstraintRepository.GetByIdAsync(constraintId).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.DeleteActivityConstraintAsync(organizationId, constraintId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public void DeleteActivityConstraintAsync_WithWrongOrganization_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var wrongOrgId = Guid.NewGuid();
        var constraintId = Guid.NewGuid();
        var constraint = new ActivityConstraint
        {
            Id = constraintId,
            OrganizationId = wrongOrgId,
            ActivityId = Guid.NewGuid(),
            Key = "key",
            Value = """{"value": true}"""
        };

        _activityConstraintRepository.GetByIdAsync(constraintId).Returns(constraint);

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.DeleteActivityConstraintAsync(organizationId, constraintId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion
}
