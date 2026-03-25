using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Schedule;
using Chronos.MainApi.Schedule.Services;
using Chronos.MainApi.Shared.ExternalMangement;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Chronos.Tests.MainApi.Services.Schedule;

[TestFixture]
public class UserPreferenceServiceTests
{
    private IUserPreferenceRepository _userPreferenceRepository = null!;
    private IManagementExternalService _validationService = null!;
    private ILogger<UserPreferenceService> _logger = null!;
    private UserPreferenceService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _userPreferenceRepository = Substitute.For<IUserPreferenceRepository>();
        _validationService = Substitute.For<IManagementExternalService>();
        _logger = Substitute.For<ILogger<UserPreferenceService>>();

        _service = new UserPreferenceService(
            _userPreferenceRepository,
            _logger,
            _validationService);
    }

    #region CreateUserPreferenceAsync Tests

    [Test]
    public async Task CreateUserPreferenceAsync_WithValidData_ReturnsNewId()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var key = "preferred_time";
        var value = "morning";

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);

        var result = await _service.CreateUserPreferenceAsync(organizationId, userId, schedulingPeriodId, key, value);

        Assert.That(result, Is.Not.EqualTo(Guid.Empty));
        await _userPreferenceRepository.Received(1).AddAsync(Arg.Is<UserPreference>(p =>
            p.OrganizationId == organizationId &&
            p.UserId == userId &&
            p.SchedulingPeriodId == schedulingPeriodId &&
            p.Key == key &&
            p.Value == value));
    }

    [Test]
    public async Task CreateUserPreferenceAsync_ValidatesOrganization()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);

        await _service.CreateUserPreferenceAsync(organizationId, userId, schedulingPeriodId, "key", "value");

        await _validationService.Received(1).ValidateOrganizationAsync(organizationId);
    }

    #endregion

    #region GetUserPreferenceAsync Tests

    [Test]
    public async Task GetUserPreferenceAsync_WithExistingPreference_ReturnsPreference()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var preferenceId = Guid.NewGuid();
        var preference = new UserPreference
        {
            Id = preferenceId,
            OrganizationId = organizationId,
            UserId = userId,
            SchedulingPeriodId = schedulingPeriodId,
            Key = "preferred_time",
            Value = "morning"
        };

        _userPreferenceRepository.GetByIdAsync(schedulingPeriodId).Returns(preference);

        var result = await _service.GetUserPreferenceAsync(organizationId, userId, schedulingPeriodId, "preferred_time");

        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void GetUserPreferenceAsync_WithNonExistentPreference_ThrowsKeyNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();

        _userPreferenceRepository.GetByIdAsync(schedulingPeriodId).ReturnsNull();

        var ex = Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await _service.GetUserPreferenceAsync(organizationId, userId, schedulingPeriodId, "key"));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public void GetUserPreferenceAsync_WithWrongOrganization_ThrowsKeyNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var wrongOrgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var preference = new UserPreference
        {
            Id = Guid.NewGuid(),
            OrganizationId = wrongOrgId,
            UserId = userId,
            SchedulingPeriodId = schedulingPeriodId,
            Key = "preferred_time",
            Value = "morning"
        };

        _userPreferenceRepository.GetByIdAsync(schedulingPeriodId).Returns(preference);

        var ex = Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await _service.GetUserPreferenceAsync(organizationId, userId, schedulingPeriodId, "key"));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion

    #region GetAllUserPreferencesAsync Tests

    [Test]
    public async Task GetAllUserPreferencesAsync_ReturnsOnlyOrganizationPreferences()
    {
        var organizationId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var preferences = new List<UserPreference>
        {
            new UserPreference
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = Guid.NewGuid(),
                SchedulingPeriodId = Guid.NewGuid(),
                Key = "pref1",
                Value = "value1"
            },
            new UserPreference
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = Guid.NewGuid(),
                SchedulingPeriodId = Guid.NewGuid(),
                Key = "pref2",
                Value = "value2"
            },
            new UserPreference
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrgId,
                UserId = Guid.NewGuid(),
                SchedulingPeriodId = Guid.NewGuid(),
                Key = "pref3",
                Value = "value3"
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _userPreferenceRepository.GetAllAsync().Returns(preferences);

        var result = await _service.GetAllUserPreferencesAsync(organizationId);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.All(p => p.OrganizationId == organizationId), Is.True);
    }

    [Test]
    public async Task GetAllUserPreferencesAsync_WithNoPreferences_ReturnsEmptyList()
    {
        var organizationId = Guid.NewGuid();

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _userPreferenceRepository.GetAllAsync().Returns(new List<UserPreference>());

        var result = await _service.GetAllUserPreferencesAsync(organizationId);

        Assert.That(result, Is.Empty);
    }

    #endregion

    #region GetAllUserPreferencesByUserIdAsync Tests

    [Test]
    public async Task GetAllUserPreferencesByUserIdAsync_ReturnsPreferencesForUser()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var preferences = new List<UserPreference>
        {
            new UserPreference
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId,
                SchedulingPeriodId = Guid.NewGuid(),
                Key = "pref1",
                Value = "value1"
            },
            new UserPreference
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId,
                SchedulingPeriodId = Guid.NewGuid(),
                Key = "pref2",
                Value = "value2"
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _userPreferenceRepository.GetByUserIdAsync(userId).Returns(preferences);

        var result = await _service.GetAllUserPreferencesByUserIdAsync(organizationId, userId);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetAllUserPreferencesByUserIdAsync_FiltersOutOtherOrganizationPreferences()
    {
        var organizationId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var preferences = new List<UserPreference>
        {
            new UserPreference
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId,
                SchedulingPeriodId = Guid.NewGuid(),
                Key = "pref1",
                Value = "value1"
            },
            new UserPreference
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrgId,
                UserId = userId,
                SchedulingPeriodId = Guid.NewGuid(),
                Key = "pref2",
                Value = "value2"
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _userPreferenceRepository.GetByUserIdAsync(userId).Returns(preferences);

        var result = await _service.GetAllUserPreferencesByUserIdAsync(organizationId, userId);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].OrganizationId, Is.EqualTo(organizationId));
    }

    #endregion

    #region GetAllUserPreferencesBySchedulingPeriodIdAsync Tests

    [Test]
    public async Task GetAllUserPreferencesBySchedulingPeriodIdAsync_ReturnsPreferencesForPeriod()
    {
        var organizationId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var preferences = new List<UserPreference>
        {
            new UserPreference
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = Guid.NewGuid(),
                SchedulingPeriodId = schedulingPeriodId,
                Key = "pref1",
                Value = "value1"
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _userPreferenceRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId).Returns(preferences);

        var result = await _service.GetAllUserPreferencesBySchedulingPeriodIdAsync(organizationId, schedulingPeriodId);

        Assert.That(result, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetAllUserPreferencesBySchedulingPeriodIdAsync_FiltersOutOtherOrganizationPreferences()
    {
        var organizationId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var preferences = new List<UserPreference>
        {
            new UserPreference
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = Guid.NewGuid(),
                SchedulingPeriodId = schedulingPeriodId,
                Key = "pref1",
                Value = "value1"
            },
            new UserPreference
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrgId,
                UserId = Guid.NewGuid(),
                SchedulingPeriodId = schedulingPeriodId,
                Key = "pref2",
                Value = "value2"
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _userPreferenceRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId).Returns(preferences);

        var result = await _service.GetAllUserPreferencesBySchedulingPeriodIdAsync(organizationId, schedulingPeriodId);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].OrganizationId, Is.EqualTo(organizationId));
    }

    #endregion

    #region GetAllUserPreferencesByUserAndPeriodAsync Tests

    [Test]
    public async Task GetAllUserPreferencesByUserAndPeriodAsync_ReturnsPreferencesForUserAndPeriod()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var preferences = new List<UserPreference>
        {
            new UserPreference
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId,
                SchedulingPeriodId = schedulingPeriodId,
                Key = "pref1",
                Value = "value1"
            },
            new UserPreference
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId,
                SchedulingPeriodId = schedulingPeriodId,
                Key = "pref2",
                Value = "value2"
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _userPreferenceRepository.GetByUserPeriodAsync(userId, schedulingPeriodId).Returns(preferences);

        var result = await _service.GetAllUserPreferencesByUserAndPeriodAsync(organizationId, userId, schedulingPeriodId);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetAllUserPreferencesByUserAndPeriodAsync_FiltersOutOtherOrganizationPreferences()
    {
        var organizationId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var preferences = new List<UserPreference>
        {
            new UserPreference
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId,
                SchedulingPeriodId = schedulingPeriodId,
                Key = "pref1",
                Value = "value1"
            },
            new UserPreference
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrgId,
                UserId = userId,
                SchedulingPeriodId = schedulingPeriodId,
                Key = "pref2",
                Value = "value2"
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _userPreferenceRepository.GetByUserPeriodAsync(userId, schedulingPeriodId).Returns(preferences);

        var result = await _service.GetAllUserPreferencesByUserAndPeriodAsync(organizationId, userId, schedulingPeriodId);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].OrganizationId, Is.EqualTo(organizationId));
    }

    #endregion

    #region UpdateUserPreferenceAsync Tests

    [Test]
    public async Task UpdateUserPreferenceAsync_WithValidData_UpdatesPreference()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var preferenceId = Guid.NewGuid();
        var preference = new UserPreference
        {
            Id = preferenceId,
            OrganizationId = organizationId,
            UserId = userId,
            SchedulingPeriodId = schedulingPeriodId,
            Key = "old_key",
            Value = "old_value"
        };

        _userPreferenceRepository.GetByIdAsync(schedulingPeriodId).Returns(preference);

        var newValue = "new_value";

        await _service.UpdateUserPreferenceAsync(organizationId, userId, schedulingPeriodId, "key", newValue);

        await _userPreferenceRepository.Received(1).UpdateAsync(Arg.Is<UserPreference>(p =>
            p.Value == newValue));
    }

    [Test]
    public void UpdateUserPreferenceAsync_WithNonExistentPreference_ThrowsKeyNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();

        _userPreferenceRepository.GetByIdAsync(schedulingPeriodId).ReturnsNull();

        var ex = Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await _service.UpdateUserPreferenceAsync(organizationId, userId, schedulingPeriodId, "key", "value"));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public void UpdateUserPreferenceAsync_WithWrongOrganization_ThrowsKeyNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var wrongOrgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var preference = new UserPreference
        {
            Id = Guid.NewGuid(),
            OrganizationId = wrongOrgId,
            UserId = userId,
            SchedulingPeriodId = schedulingPeriodId,
            Key = "key",
            Value = "value"
        };

        _userPreferenceRepository.GetByIdAsync(schedulingPeriodId).Returns(preference);

        var ex = Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await _service.UpdateUserPreferenceAsync(organizationId, userId, schedulingPeriodId, "key", "new_value"));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion

    #region DeleteUserPreferenceAsync Tests

    [Test]
    public async Task DeleteUserPreferenceAsync_WithExistingPreference_DeletesPreference()
    {
        var organizationId = Guid.NewGuid();
        var preferenceId = Guid.NewGuid();
        var preference = new UserPreference
        {
            Id = preferenceId,
            OrganizationId = organizationId,
            UserId = Guid.NewGuid(),
            SchedulingPeriodId = Guid.NewGuid(),
            Key = "key",
            Value = "value"
        };

        _userPreferenceRepository.GetByIdAsync(preferenceId).Returns(preference);

        await _service.DeleteUserPreferenceAsync(organizationId, preferenceId);

        await _userPreferenceRepository.Received(1).DeleteAsync(preference);
    }

    [Test]
    public void DeleteUserPreferenceAsync_WithNonExistentPreference_ThrowsKeyNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var preferenceId = Guid.NewGuid();

        _userPreferenceRepository.GetByIdAsync(preferenceId).ReturnsNull();

        var ex = Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await _service.DeleteUserPreferenceAsync(organizationId, preferenceId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public void DeleteUserPreferenceAsync_WithWrongOrganization_ThrowsKeyNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var wrongOrgId = Guid.NewGuid();
        var preferenceId = Guid.NewGuid();
        var preference = new UserPreference
        {
            Id = preferenceId,
            OrganizationId = wrongOrgId,
            UserId = Guid.NewGuid(),
            SchedulingPeriodId = Guid.NewGuid(),
            Key = "key",
            Value = "value"
        };

        _userPreferenceRepository.GetByIdAsync(preferenceId).Returns(preference);

        var ex = Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await _service.DeleteUserPreferenceAsync(organizationId, preferenceId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion
}
