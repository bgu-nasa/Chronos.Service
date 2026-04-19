using Chronos.Data.Repositories.Management;
using Chronos.Domain.Management;
using Chronos.MainApi.Management.Services;
using Chronos.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Chronos.Tests.MainApi.Services.Management;

[TestFixture]
[Category("Unit")]
public class ManagementValidationServiceTests
{
    private IOrganizationRepository _organizationRepository = null!;
    private IDepartmentRepository _departmentRepository = null!;
    private ManagementValidationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _departmentRepository = Substitute.For<IDepartmentRepository>();
        _service = new ManagementValidationService(
            _organizationRepository,
            _departmentRepository,
            Substitute.For<ILogger<ManagementValidationService>>());
    }

    [Test]
    public void GivenNullOrganization_WhenValidateOrganization_ThenThrowsNotFound()
    {
        _organizationRepository.GetByIdAsync(Arg.Any<Guid>()).ReturnsNull();

        Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ValidateOrganizationAsync(Guid.NewGuid()));
    }

    [Test]
    public void GivenDeletedOrganization_WhenValidateOrganization_ThenThrowsNotFound()
    {
        var orgId = Guid.NewGuid();
        _organizationRepository.GetByIdAsync(orgId)
            .Returns(new Organization { Id = orgId, Deleted = true });

        Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ValidateOrganizationAsync(orgId));
    }

    [Test]
    public void GivenActiveOrganization_WhenValidateOrganization_ThenDoesNotThrow()
    {
        var orgId = Guid.NewGuid();
        _organizationRepository.GetByIdAsync(orgId)
            .Returns(new Organization { Id = orgId, Deleted = false });

        Assert.DoesNotThrowAsync(() =>
            _service.ValidateOrganizationAsync(orgId));
    }

    [Test]
    public void GivenNullDepartment_WhenValidateAndGetDepartment_ThenThrowsNotFound()
    {
        _departmentRepository.GetByIdAsync(Arg.Any<Guid>()).ReturnsNull();

        Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ValidateAndGetDepartmentAsync(Guid.NewGuid(), Guid.NewGuid(), false));
    }

    [Test]
    public void GivenDepartmentFromDifferentOrg_WhenValidateAndGetDepartment_ThenThrowsNotFound()
    {
        var deptId = Guid.NewGuid();
        _departmentRepository.GetByIdAsync(deptId)
            .Returns(new Department { Id = deptId, OrganizationId = Guid.NewGuid(), Deleted = false });

        Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ValidateAndGetDepartmentAsync(Guid.NewGuid(), deptId, false));
    }

    [Test]
    public void GivenDeletedDepartment_WhenValidateWithExcludeDeleted_ThenThrowsNotFound()
    {
        var orgId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        _departmentRepository.GetByIdAsync(deptId)
            .Returns(new Department { Id = deptId, OrganizationId = orgId, Deleted = true });

        Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ValidateAndGetDepartmentAsync(orgId, deptId, excludeDeleted: true));
    }

    [Test]
    public async Task GivenDeletedDepartment_WhenValidateWithoutExcludeDeleted_ThenReturnsDepartment()
    {
        var orgId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        _departmentRepository.GetByIdAsync(deptId)
            .Returns(new Department { Id = deptId, OrganizationId = orgId, Deleted = true });

        var result = await _service.ValidateAndGetDepartmentAsync(orgId, deptId, excludeDeleted: false);

        Assert.That(result.Id, Is.EqualTo(deptId));
    }
}
