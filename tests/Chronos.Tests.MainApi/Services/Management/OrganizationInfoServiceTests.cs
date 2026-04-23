using Chronos.Domain.Management;
using Chronos.MainApi.Auth.Contracts;
using Chronos.MainApi.Management.Contracts;
using Chronos.MainApi.Management.Services;
using Chronos.MainApi.Management.Services.External;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Chronos.Tests.MainApi.Services.Management;

[TestFixture]
[Category("Unit")]
public class OrganizationInfoServiceTests
{
    private IOrganizationService _organizationService = null!;
    private IDepartmentService _departmentService = null!;
    private IAuthClient _authClient = null!;
    private IRoleService _roleService = null!;
    private OrganizationInfoService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _organizationService = Substitute.For<IOrganizationService>();
        _departmentService = Substitute.For<IDepartmentService>();
        _authClient = Substitute.For<IAuthClient>();
        _roleService = Substitute.For<IRoleService>();

        _service = new OrganizationInfoService(
            Substitute.For<ILogger<OrganizationInfoService>>(),
            _organizationService,
            _departmentService,
            _authClient,
            _roleService);
    }

    [Test]
    public async Task GivenFullOrgData_WhenGetOrganizationInfo_ThenAssemblesCorrectDto()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _organizationService.GetOrganizationAsync(orgId)
            .Returns(new Organization { Id = orgId, Name = "Acme", Deleted = false });

        _departmentService.GetDepartmentsAsync(orgId)
            .Returns(new List<Department>
            {
                new() { Id = Guid.NewGuid(), Name = "Engineering", Deleted = false }
            });

        _roleService.GetUserAssignmentsAsync(orgId, userId)
            .Returns(new List<RoleAssignmentResponse>
            {
                new(Guid.NewGuid(), userId, orgId, null, RoleType.Administrator)
            });

        _authClient.GetUserAsync(orgId, userId)
            .Returns(new UserResponse(userId.ToString(), "admin@acme.com", "John", "Doe", null, true));

        var result = await _service.GetOrganizationInformationAsync(orgId, userId);

        Assert.That(result.Name, Is.EqualTo("Acme"));
        Assert.That(result.UserEmail, Is.EqualTo("admin@acme.com"));
        Assert.That(result.UserFullName, Is.EqualTo("John Doe"));
        Assert.That(result.Departments, Has.Length.EqualTo(1));
        Assert.That(result.UserRoles, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task GivenNoDepartmentsOrRoles_WhenGetOrganizationInfo_ThenReturnsEmptyArrays()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _organizationService.GetOrganizationAsync(orgId)
            .Returns(new Organization { Id = orgId, Name = "Empty Corp", Deleted = false });

        _departmentService.GetDepartmentsAsync(orgId)
            .Returns(new List<Department>());

        _roleService.GetUserAssignmentsAsync(orgId, userId)
            .Returns(new List<RoleAssignmentResponse>());

        _authClient.GetUserAsync(orgId, userId)
            .Returns(new UserResponse(userId.ToString(), "user@test.com", "Jane", "Smith", null, true));

        var result = await _service.GetOrganizationInformationAsync(orgId, userId);

        Assert.That(result.Departments, Is.Empty);
        Assert.That(result.UserRoles, Is.Empty);
    }
}
