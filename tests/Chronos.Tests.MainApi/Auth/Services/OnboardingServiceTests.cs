using Chronos.Domain.Auth;
using Chronos.Domain.Management.Roles;
using Chronos.MainApi.Auth.Services;
using Chronos.MainApi.Management.Contracts;
using Chronos.MainApi.Management.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Chronos.Tests.MainApi.Auth.Services;

[TestFixture]
[Category("Unit")]
public class OnboardingServiceTests
{
    private ILogger<OnboardingService> _logger;
    private IOrganizationService _organizationService;
    private IRoleService _roleService;
    private OnboardingService _sut;

    [SetUp]
    public void SetUp()
    {
        _logger = Substitute.For<ILogger<OnboardingService>>();
        _organizationService = Substitute.For<IOrganizationService>();
        _roleService = Substitute.For<IRoleService>();
        _sut = new OnboardingService(_logger, _organizationService, _roleService);
    }

    [Test]
    public async Task GivenOrgName_WhenCreateOrganization_ThenDelegatesToOrganizationService()
    {
        var expectedId = Guid.NewGuid();
        _organizationService.CreateOrganizationAsync("MyOrg").Returns(expectedId);

        var result = await _sut.CreateOrganizationAsync("MyOrg", "Enterprise");

        Assert.That(result, Is.EqualTo(expectedId));
        await _organizationService.Received(1).CreateOrganizationAsync("MyOrg");
    }

    [Test]
    public async Task GivenAdminUser_WhenOnboardAdmin_ThenAssignsAdministratorRole()
    {
        var orgId = Guid.NewGuid();
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            FirstName = "Admin",
            LastName = "User",
            PasswordHash = "hash",
            OrganizationId = orgId
        };

        _roleService.AddAssignmentAsync(orgId, null, admin.Id, Role.Administrator, isSystemAssigned: true)
            .Returns(new RoleAssignmentResponse(Guid.NewGuid(), admin.Id, orgId, null, RoleType.Administrator));

        await _sut.OnboardAdminUserAsync(orgId, admin);

        await _roleService.Received(1).AddAssignmentAsync(
            orgId, null, admin.Id, Role.Administrator, isSystemAssigned: true);
    }
}
