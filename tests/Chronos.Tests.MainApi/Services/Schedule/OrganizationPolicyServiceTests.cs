using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Schedule;
using Chronos.MainApi.Schedule.Services;
using Chronos.MainApi.Shared.ExternalMangement;
using Chronos.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Chronos.Tests.MainApi.Services.Schedule;

[TestFixture]
public class OrganizationPolicyServiceTests
{
    private IOrganizationPolicyRepository _organizationPolicyRepository = null!;
    private IManagementExternalService _validationService = null!;
    private ISchedulingPeriodService _schedulingPeriodService = null!;
    private ILogger<OrganizationPolicyService> _logger = null!;
    private OrganizationPolicyService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _organizationPolicyRepository = Substitute.For<IOrganizationPolicyRepository>();
        _validationService = Substitute.For<IManagementExternalService>();
        _schedulingPeriodService = Substitute.For<ISchedulingPeriodService>();
        _logger = Substitute.For<ILogger<OrganizationPolicyService>>();

        _service = new OrganizationPolicyService(
            _organizationPolicyRepository,
            _validationService,
            _schedulingPeriodService,
            _logger);
    }

    #region CreatePolicyAsync Tests

    [Test]
    public async Task CreatePolicyAsync_WithValidData_ReturnsNewPolicy()
    {
        var organizationId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var key = "max_hours_per_day";
        var value = "8";

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);

        var result = await _service.CreatePolicyAsync(organizationId, schedulingPeriodId, key, value);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(result.OrganizationId, Is.EqualTo(organizationId));
        Assert.That(result.SchedulingPeriodId, Is.EqualTo(schedulingPeriodId));
        Assert.That(result.Key, Is.EqualTo(key));
        Assert.That(result.Value, Is.EqualTo(value));

        await _organizationPolicyRepository.Received(1).AddAsync(Arg.Is<OrganizationPolicy>(p =>
            p.OrganizationId == organizationId &&
            p.SchedulingPeriodId == schedulingPeriodId &&
            p.Key == key &&
            p.Value == value));
    }

    [Test]
    public async Task CreatePolicyAsync_ValidatesOrganization()
    {
        var organizationId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);

        await _service.CreatePolicyAsync(organizationId, schedulingPeriodId, "key", "value");

        await _validationService.Received(1).ValidateOrganizationAsync(organizationId);
    }

    #endregion

    #region GetPolicyAsync Tests

    [Test]
    public async Task GetPolicyAsync_WithExistingPolicy_ReturnsPolicy()
    {
        var organizationId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var policy = new OrganizationPolicy
        {
            Id = policyId,
            OrganizationId = organizationId,
            SchedulingPeriodId = Guid.NewGuid(),
            Key = "max_hours",
            Value = "8"
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _organizationPolicyRepository.GetByIdAsync(policyId).Returns(policy);

        var result = await _service.GetPolicyAsync(organizationId, policyId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(policyId));
    }

    [Test]
    public void GetPolicyAsync_WithNonExistentPolicy_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var policyId = Guid.NewGuid();

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _organizationPolicyRepository.GetByIdAsync(policyId).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.GetPolicyAsync(organizationId, policyId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public void GetPolicyAsync_WithWrongOrganization_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var wrongOrgId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var policy = new OrganizationPolicy
        {
            Id = policyId,
            OrganizationId = wrongOrgId,
            SchedulingPeriodId = Guid.NewGuid(),
            Key = "max_hours",
            Value = "8"
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _organizationPolicyRepository.GetByIdAsync(policyId).Returns(policy);

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.GetPolicyAsync(organizationId, policyId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion

    #region GetAllPoliciesAsync Tests

    [Test]
    public async Task GetAllPoliciesAsync_ReturnsOnlyOrganizationPolicies()
    {
        var organizationId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var policies = new List<OrganizationPolicy>
        {
            new OrganizationPolicy
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SchedulingPeriodId = Guid.NewGuid(),
                Key = "policy1",
                Value = "value1"
            },
            new OrganizationPolicy
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SchedulingPeriodId = Guid.NewGuid(),
                Key = "policy2",
                Value = "value2"
            },
            new OrganizationPolicy
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrgId,
                SchedulingPeriodId = Guid.NewGuid(),
                Key = "policy3",
                Value = "value3"
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _organizationPolicyRepository.GetAllAsync().Returns(policies);

        var result = await _service.GetAllPoliciesAsync(organizationId);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.All(p => p.OrganizationId == organizationId), Is.True);
    }

    [Test]
    public async Task GetAllPoliciesAsync_WithNoPolicies_ReturnsEmptyList()
    {
        var organizationId = Guid.NewGuid();

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _organizationPolicyRepository.GetAllAsync().Returns(new List<OrganizationPolicy>());

        var result = await _service.GetAllPoliciesAsync(organizationId);

        Assert.That(result, Is.Empty);
    }

    #endregion

    #region GetPoliciesBySchedulingPeriodIdsAsync Tests

    [Test]
    public async Task GetPoliciesBySchedulingPeriodIdsAsync_ReturnsFilteredPolicies()
    {
        var organizationId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var policies = new List<OrganizationPolicy>
        {
            new OrganizationPolicy
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SchedulingPeriodId = schedulingPeriodId,
                Key = "policy1",
                Value = "value1"
            },
            new OrganizationPolicy
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SchedulingPeriodId = schedulingPeriodId,
                Key = "policy2",
                Value = "value2"
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _organizationPolicyRepository.GetByPeriodAsync(schedulingPeriodId).Returns(policies);

        var result = await _service.GetPoliciesBySchedulingPeriodIdsAsync(organizationId, schedulingPeriodId);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetPoliciesBySchedulingPeriodIdsAsync_FiltersOutOtherOrganizationPolicies()
    {
        var organizationId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var policies = new List<OrganizationPolicy>
        {
            new OrganizationPolicy
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SchedulingPeriodId = schedulingPeriodId,
                Key = "policy1",
                Value = "value1"
            },
            new OrganizationPolicy
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrgId,
                SchedulingPeriodId = schedulingPeriodId,
                Key = "policy2",
                Value = "value2"
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _organizationPolicyRepository.GetByPeriodAsync(schedulingPeriodId).Returns(policies);

        var result = await _service.GetPoliciesBySchedulingPeriodIdsAsync(organizationId, schedulingPeriodId);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].OrganizationId, Is.EqualTo(organizationId));
    }

    #endregion

    #region UpdatePolicyAsync Tests

    [Test]
    public async Task UpdatePolicyAsync_WithValidData_UpdatesPolicy()
    {
        var organizationId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var policy = new OrganizationPolicy
        {
            Id = policyId,
            OrganizationId = organizationId,
            SchedulingPeriodId = Guid.NewGuid(),
            Key = "old_key",
            Value = "old_value"
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _organizationPolicyRepository.GetByIdAsync(policyId).Returns(policy);

        var newKey = "new_key";
        var newValue = "new_value";

        var result = await _service.UpdatePolicyAsync(organizationId, policyId, newKey, newValue);

        Assert.That(result.Key, Is.EqualTo(newKey));
        Assert.That(result.Value, Is.EqualTo(newValue));
        await _organizationPolicyRepository.Received(1).UpdateAsync(Arg.Is<OrganizationPolicy>(p =>
            p.Key == newKey && p.Value == newValue));
    }

    [Test]
    public void UpdatePolicyAsync_WithNonExistentPolicy_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var policyId = Guid.NewGuid();

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _organizationPolicyRepository.GetByIdAsync(policyId).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.UpdatePolicyAsync(organizationId, policyId, "key", "value"));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public void UpdatePolicyAsync_WithWrongOrganization_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var wrongOrgId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var policy = new OrganizationPolicy
        {
            Id = policyId,
            OrganizationId = wrongOrgId,
            SchedulingPeriodId = Guid.NewGuid(),
            Key = "key",
            Value = "value"
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _organizationPolicyRepository.GetByIdAsync(policyId).Returns(policy);

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.UpdatePolicyAsync(organizationId, policyId, "new_key", "new_value"));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion

    #region DeletePolicyAsync Tests

    [Test]
    public async Task DeletePolicyAsync_WithExistingPolicy_DeletesPolicy()
    {
        var organizationId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var policy = new OrganizationPolicy
        {
            Id = policyId,
            OrganizationId = organizationId,
            SchedulingPeriodId = Guid.NewGuid(),
            Key = "key",
            Value = "value"
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _organizationPolicyRepository.GetByIdAsync(policyId).Returns(policy);

        await _service.DeletePolicyAsync(organizationId, policyId);

        await _organizationPolicyRepository.Received(1).DeleteAsync(policy);
    }

    [Test]
    public void DeletePolicyAsync_WithNonExistentPolicy_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var policyId = Guid.NewGuid();

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _organizationPolicyRepository.GetByIdAsync(policyId).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.DeletePolicyAsync(organizationId, policyId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public void DeletePolicyAsync_WithWrongOrganization_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var wrongOrgId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var policy = new OrganizationPolicy
        {
            Id = policyId,
            OrganizationId = wrongOrgId,
            SchedulingPeriodId = Guid.NewGuid(),
            Key = "key",
            Value = "value"
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _organizationPolicyRepository.GetByIdAsync(policyId).Returns(policy);

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.DeletePolicyAsync(organizationId, policyId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion
}
