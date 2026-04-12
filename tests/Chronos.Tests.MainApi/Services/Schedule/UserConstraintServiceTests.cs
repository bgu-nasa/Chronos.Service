using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Schedule;
using Chronos.MainApi.Schedule.Messaging;
using Chronos.MainApi.Schedule.Services;
using Chronos.MainApi.Shared.ExternalMangement;
using Chronos.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Chronos.Tests.MainApi.Services.Schedule;

[TestFixture]
public class UserConstraintServiceTests
{
    private IUserConstraintRepository _userConstraintRepository = null!;
    private IManagementExternalService _validationService = null!;
    private ISchedulingPeriodService _schedulingPeriodService = null!;
    private IMessagePublisher _messagePublisher = null!;
    private ILogger<UserConstraintService> _logger = null!;
    private UserConstraintService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _userConstraintRepository = Substitute.For<IUserConstraintRepository>();
        _validationService = Substitute.For<IManagementExternalService>();
        _schedulingPeriodService = Substitute.For<ISchedulingPeriodService>();
        _messagePublisher = Substitute.For<IMessagePublisher>();
        _logger = Substitute.For<ILogger<UserConstraintService>>();

        _service = new UserConstraintService(
            _userConstraintRepository,
            _validationService,
            _schedulingPeriodService,
            _logger,
            _messagePublisher);
    }

    #region CreateUserConstraintAsync Tests

    [Test]
    public async Task CreateUserConstraintAsync_WithValidData_ReturnsNewId()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var key = "availability";
        var value = "morning";

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);

        var result = await _service.CreateUserConstraintAsync(organizationId, userId, schedulingPeriodId, key, value);

        Assert.That(result, Is.Not.EqualTo(Guid.Empty));
        await _userConstraintRepository.Received(1).AddAsync(Arg.Is<UserConstraint>(c =>
            c.OrganizationId == organizationId &&
            c.UserId == userId &&
            c.SchedulingPeriodId == schedulingPeriodId &&
            c.Key == key &&
            c.Value == value));
        await _messagePublisher.Received(1).PublishAsync(Arg.Any<Chronos.Domain.Schedule.Messages.HandleConstraintChangeRequest>(), "request.online");
    }

    [Test]
    public async Task CreateUserConstraintAsync_ValidatesOrganization()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);

        await _service.CreateUserConstraintAsync(organizationId, userId, schedulingPeriodId, "key", "value");

        await _validationService.Received(1).ValidateOrganizationAsync(organizationId);
    }

    #endregion

    #region GetUserConstraintByIdAsync Tests

    [Test]
    public async Task GetUserConstraintByIdAsync_WithExistingConstraint_ReturnsConstraint()
    {
        var organizationId = Guid.NewGuid();
        var constraintId = Guid.NewGuid();
        var constraint = new UserConstraint
        {
            Id = constraintId,
            OrganizationId = organizationId,
            UserId = Guid.NewGuid(),
            SchedulingPeriodId = Guid.NewGuid(),
            Key = "availability",
            Value = "morning"
        };

        _userConstraintRepository.GetByIdAsync(constraintId).Returns(constraint);

        var result = await _service.GetUserConstraintByIdAsync(organizationId, constraintId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(constraintId));
    }

    [Test]
    public void GetUserConstraintByIdAsync_WithNonExistentConstraint_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var constraintId = Guid.NewGuid();

        _userConstraintRepository.GetByIdAsync(constraintId).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.GetUserConstraintByIdAsync(organizationId, constraintId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public void GetUserConstraintByIdAsync_WithWrongOrganization_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var wrongOrgId = Guid.NewGuid();
        var constraintId = Guid.NewGuid();
        var constraint = new UserConstraint
        {
            Id = constraintId,
            OrganizationId = wrongOrgId,
            UserId = Guid.NewGuid(),
            SchedulingPeriodId = Guid.NewGuid(),
            Key = "availability",
            Value = "morning"
        };

        _userConstraintRepository.GetByIdAsync(constraintId).Returns(constraint);

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.GetUserConstraintByIdAsync(organizationId, constraintId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion

    #region GetAllUserConstraintsAsync Tests

    [Test]
    public async Task GetAllUserConstraintsAsync_ReturnsOnlyOrganizationConstraints()
    {
        var organizationId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var constraints = new List<UserConstraint>
        {
            new UserConstraint
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = Guid.NewGuid(),
                SchedulingPeriodId = Guid.NewGuid(),
                Key = "constraint1",
                Value = "value1"
            },
            new UserConstraint
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = Guid.NewGuid(),
                SchedulingPeriodId = Guid.NewGuid(),
                Key = "constraint2",
                Value = "value2"
            },
            new UserConstraint
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrgId,
                UserId = Guid.NewGuid(),
                SchedulingPeriodId = Guid.NewGuid(),
                Key = "constraint3",
                Value = "value3"
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _userConstraintRepository.GetAllAsync().Returns(constraints);

        var result = await _service.GetAllUserConstraintsAsync(organizationId);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.All(c => c.OrganizationId == organizationId), Is.True);
    }

    [Test]
    public async Task GetAllUserConstraintsAsync_WithNoConstraints_ReturnsEmptyList()
    {
        var organizationId = Guid.NewGuid();

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _userConstraintRepository.GetAllAsync().Returns(new List<UserConstraint>());

        var result = await _service.GetAllUserConstraintsAsync(organizationId);

        Assert.That(result, Is.Empty);
    }

    #endregion

    #region GetByUserIdAsync Tests

    [Test]
    public async Task GetByUserIdAsync_ReturnsConstraintsForUser()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var constraints = new List<UserConstraint>
        {
            new UserConstraint
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId,
                SchedulingPeriodId = Guid.NewGuid(),
                Key = "constraint1",
                Value = "value1"
            },
            new UserConstraint
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId,
                SchedulingPeriodId = Guid.NewGuid(),
                Key = "constraint2",
                Value = "value2"
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _userConstraintRepository.GetByUserIdAsync(userId).Returns(constraints);

        var result = await _service.GetByUserIdAsync(organizationId, userId);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetByUserIdAsync_FiltersOutOtherOrganizationConstraints()
    {
        var organizationId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var constraints = new List<UserConstraint>
        {
            new UserConstraint
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId,
                SchedulingPeriodId = Guid.NewGuid(),
                Key = "constraint1",
                Value = "value1"
            },
            new UserConstraint
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrgId,
                UserId = userId,
                SchedulingPeriodId = Guid.NewGuid(),
                Key = "constraint2",
                Value = "value2"
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _userConstraintRepository.GetByUserIdAsync(userId).Returns(constraints);

        var result = await _service.GetByUserIdAsync(organizationId, userId);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].OrganizationId, Is.EqualTo(organizationId));
    }

    #endregion

    #region GetBySchedulingPeriodIdAsync Tests

    [Test]
    public async Task GetBySchedulingPeriodIdAsync_ReturnsConstraintsForPeriod()
    {
        var organizationId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var constraints = new List<UserConstraint>
        {
            new UserConstraint
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = Guid.NewGuid(),
                SchedulingPeriodId = schedulingPeriodId,
                Key = "constraint1",
                Value = "value1"
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _userConstraintRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId).Returns(constraints);

        var result = await _service.GetBySchedulingPeriodIdAsync(organizationId, schedulingPeriodId);

        Assert.That(result, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetBySchedulingPeriodIdAsync_FiltersOutOtherOrganizationConstraints()
    {
        var organizationId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var constraints = new List<UserConstraint>
        {
            new UserConstraint
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = Guid.NewGuid(),
                SchedulingPeriodId = schedulingPeriodId,
                Key = "constraint1",
                Value = "value1"
            },
            new UserConstraint
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrgId,
                UserId = Guid.NewGuid(),
                SchedulingPeriodId = schedulingPeriodId,
                Key = "constraint2",
                Value = "value2"
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _userConstraintRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId).Returns(constraints);

        var result = await _service.GetBySchedulingPeriodIdAsync(organizationId, schedulingPeriodId);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].OrganizationId, Is.EqualTo(organizationId));
    }

    #endregion

    #region GetBySchedulingPeriodAndUserIdAsync Tests

    [Test]
    public async Task GetBySchedulingPeriodAndUserIdAsync_ReturnsConstraintsForUserAndPeriod()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var constraints = new List<UserConstraint>
        {
            new UserConstraint
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId,
                SchedulingPeriodId = schedulingPeriodId,
                Key = "constraint1",
                Value = "value1"
            },
            new UserConstraint
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId,
                SchedulingPeriodId = schedulingPeriodId,
                Key = "constraint2",
                Value = "value2"
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _userConstraintRepository.GetByUserPeriodAsync(userId, schedulingPeriodId).Returns(constraints);

        var result = await _service.GetBySchedulingPeriodAndUserIdAsync(organizationId, schedulingPeriodId, userId);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetBySchedulingPeriodAndUserIdAsync_FiltersOutOtherOrganizationConstraints()
    {
        var organizationId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var constraints = new List<UserConstraint>
        {
            new UserConstraint
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId,
                SchedulingPeriodId = schedulingPeriodId,
                Key = "constraint1",
                Value = "value1"
            },
            new UserConstraint
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrgId,
                UserId = userId,
                SchedulingPeriodId = schedulingPeriodId,
                Key = "constraint2",
                Value = "value2"
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _userConstraintRepository.GetByUserPeriodAsync(userId, schedulingPeriodId).Returns(constraints);

        var result = await _service.GetBySchedulingPeriodAndUserIdAsync(organizationId, schedulingPeriodId, userId);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].OrganizationId, Is.EqualTo(organizationId));
    }

    #endregion

    #region UpdateUserConstraintAsync Tests

    [Test]
    public async Task UpdateUserConstraintAsync_WithValidData_UpdatesConstraint()
    {
        var organizationId = Guid.NewGuid();
        var constraintId = Guid.NewGuid();
        var constraint = new UserConstraint
        {
            Id = constraintId,
            OrganizationId = organizationId,
            UserId = Guid.NewGuid(),
            SchedulingPeriodId = Guid.NewGuid(),
            Key = "old_key",
            Value = "old_value"
        };

        _userConstraintRepository.GetByIdAsync(constraintId).Returns(constraint);

        var newKey = "new_key";
        var newValue = "new_value";

        await _service.UpdateUserConstraintAsync(organizationId, constraintId, newKey, newValue);

        await _userConstraintRepository.Received(1).UpdateAsync(Arg.Is<UserConstraint>(c =>
            c.Key == newKey && c.Value == newValue));
    }

    [Test]
    public void UpdateUserConstraintAsync_WithNonExistentConstraint_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var constraintId = Guid.NewGuid();

        _userConstraintRepository.GetByIdAsync(constraintId).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.UpdateUserConstraintAsync(organizationId, constraintId, "key", "value"));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public void UpdateUserConstraintAsync_WithWrongOrganization_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var wrongOrgId = Guid.NewGuid();
        var constraintId = Guid.NewGuid();
        var constraint = new UserConstraint
        {
            Id = constraintId,
            OrganizationId = wrongOrgId,
            UserId = Guid.NewGuid(),
            SchedulingPeriodId = Guid.NewGuid(),
            Key = "key",
            Value = "value"
        };

        _userConstraintRepository.GetByIdAsync(constraintId).Returns(constraint);

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.UpdateUserConstraintAsync(organizationId, constraintId, "new_key", "new_value"));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion

    #region DeleteUserConstraintAsync Tests

    [Test]
    public async Task DeleteUserConstraintAsync_WithExistingConstraint_DeletesConstraint()
    {
        var organizationId = Guid.NewGuid();
        var constraintId = Guid.NewGuid();
        var constraint = new UserConstraint
        {
            Id = constraintId,
            OrganizationId = organizationId,
            UserId = Guid.NewGuid(),
            SchedulingPeriodId = Guid.NewGuid(),
            Key = "key",
            Value = "value"
        };

        _userConstraintRepository.GetByIdAsync(constraintId).Returns(constraint);

        await _service.DeleteUserConstraintAsync(organizationId, constraintId);

        await _userConstraintRepository.Received(1).DeleteAsync(constraint);
    }

    [Test]
    public void DeleteUserConstraintAsync_WithNonExistentConstraint_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var constraintId = Guid.NewGuid();

        _userConstraintRepository.GetByIdAsync(constraintId).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.DeleteUserConstraintAsync(organizationId, constraintId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public void DeleteUserConstraintAsync_WithWrongOrganization_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var wrongOrgId = Guid.NewGuid();
        var constraintId = Guid.NewGuid();
        var constraint = new UserConstraint
        {
            Id = constraintId,
            OrganizationId = wrongOrgId,
            UserId = Guid.NewGuid(),
            SchedulingPeriodId = Guid.NewGuid(),
            Key = "key",
            Value = "value"
        };

        _userConstraintRepository.GetByIdAsync(constraintId).Returns(constraint);

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.DeleteUserConstraintAsync(organizationId, constraintId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion
}
