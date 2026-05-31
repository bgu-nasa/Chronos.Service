using Chronos.Data.Repositories.Management;
using Chronos.Domain.Management;
using Chronos.MainApi.Management.Services;
using Chronos.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Chronos.Tests.MainApi.Services.Management;

[TestFixture]
[Category("Unit")]
public class DepartmentServiceTests
{
    private IDepartmentRepository _departmentRepository = null!;
    private IOrganizationRepository _organizationRepository = null!;
    private DepartmentService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _departmentRepository = Substitute.For<IDepartmentRepository>();
        _organizationRepository = Substitute.For<IOrganizationRepository>();

        var validationService = new ManagementValidationService(
            _organizationRepository,
            _departmentRepository,
            Substitute.For<ILogger<ManagementValidationService>>());

        _service = new DepartmentService(
            _departmentRepository,
            validationService,
            Substitute.For<ILogger<DepartmentService>>());
    }

    private void SetupValidOrganization(Guid orgId)
    {
        _organizationRepository.GetByIdAsync(orgId)
            .Returns(new Organization { Id = orgId, Deleted = false });
    }

    [Test]
    public async Task GivenValidOrg_WhenCreateDepartment_ThenAddsToDepartmentRepository()
    {
        var orgId = Guid.NewGuid();
        SetupValidOrganization(orgId);

        var result = await _service.CreateDepartmentAsync(orgId, "Engineering");

        Assert.That(result.Name, Is.EqualTo("Engineering"));
        Assert.That(result.OrganizationId, Is.EqualTo(orgId));
        await _departmentRepository.Received(1).AddAsync(Arg.Any<Department>());
    }

    [Test]
    public void GivenDeletedOrg_WhenCreateDepartment_ThenThrowsNotFound()
    {
        var orgId = Guid.NewGuid();
        _organizationRepository.GetByIdAsync(orgId)
            .Returns(new Organization { Id = orgId, Deleted = true });

        Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateDepartmentAsync(orgId, "Engineering"));
    }

    [Test]
    public async Task GivenDepartmentsAcrossOrgs_WhenGetDepartments_ThenReturnsOnlyThisOrgs()
    {
        // GetAllAsync returns the whole (cross-tenant) departments table;
        // the service must return only the departments owned by the requested org.
        var orgId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        SetupValidOrganization(orgId);

        var ownA = new Department { Id = Guid.NewGuid(), OrganizationId = orgId, Deleted = false };
        var ownB = new Department { Id = Guid.NewGuid(), OrganizationId = orgId, Deleted = false };
        _departmentRepository.GetAllAsync().Returns(new List<Department>
        {
            ownA,
            new() { Id = Guid.NewGuid(), OrganizationId = otherOrgId, Deleted = false },
            ownB,
            new() { Id = Guid.NewGuid(), OrganizationId = otherOrgId, Deleted = false },
        });

        var result = await _service.GetDepartmentsAsync(orgId);

        Assert.That(result.Select(d => d.Id), Is.EquivalentTo(new[] { ownA.Id, ownB.Id }));
    }

    [Test]
    public async Task GivenDeletedDepartmentInOrg_WhenGetDepartments_ThenExcludesDeleted()
    {
        var orgId = Guid.NewGuid();
        SetupValidOrganization(orgId);

        var activeDept = new Department { Id = Guid.NewGuid(), OrganizationId = orgId, Deleted = false };
        _departmentRepository.GetAllAsync().Returns(new List<Department>
        {
            activeDept,
            new() { Id = Guid.NewGuid(), OrganizationId = orgId, Deleted = true },
        });

        var result = await _service.GetDepartmentsAsync(orgId);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo(activeDept.Id));
    }

    [Test]
    public void GivenAlreadyDeleted_WhenSetForDeletion_ThenThrowsBadRequest()
    {
        var orgId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        SetupValidOrganization(orgId);
        _departmentRepository.GetByIdAsync(deptId)
            .Returns(new Department { Id = deptId, OrganizationId = orgId, Deleted = true });

        var ex = Assert.ThrowsAsync<BadRequestException>(() =>
            _service.SetForDeletionAsync(orgId, deptId));

        Assert.That(ex!.Message, Does.Contain("already set for deletion"));
    }

    [Test]
    public async Task GivenActiveDepartment_WhenSetForDeletion_ThenMarksDeletedWithTimestamp()
    {
        var orgId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        SetupValidOrganization(orgId);
        var dept = new Department { Id = deptId, OrganizationId = orgId, Deleted = false };
        _departmentRepository.GetByIdAsync(deptId).Returns(dept);

        await _service.SetForDeletionAsync(orgId, deptId);

        Assert.That(dept.Deleted, Is.True);
        Assert.That(dept.DeletedTime, Is.Not.EqualTo(default(DateTime)));
        await _departmentRepository.Received(1).UpdateAsync(dept);
    }

    [Test]
    public void GivenNotDeletedDepartment_WhenRestore_ThenThrowsBadRequest()
    {
        var orgId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        SetupValidOrganization(orgId);
        _departmentRepository.GetByIdAsync(deptId)
            .Returns(new Department { Id = deptId, OrganizationId = orgId, Deleted = false });

        var ex = Assert.ThrowsAsync<BadRequestException>(() =>
            _service.RestoreDeletedDepartmentAsync(orgId, deptId));

        Assert.That(ex!.Message, Does.Contain("not set for deletion"));
    }

    [Test]
    public async Task GivenDeletedDepartment_WhenRestore_ThenClearsDeletedFlag()
    {
        var orgId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        SetupValidOrganization(orgId);
        var dept = new Department
        {
            Id = deptId, OrganizationId = orgId,
            Deleted = true, DeletedTime = DateTime.UtcNow
        };
        _departmentRepository.GetByIdAsync(deptId).Returns(dept);

        await _service.RestoreDeletedDepartmentAsync(orgId, deptId);

        Assert.That(dept.Deleted, Is.False);
        Assert.That(dept.DeletedTime, Is.EqualTo(default(DateTime)));
    }
}
