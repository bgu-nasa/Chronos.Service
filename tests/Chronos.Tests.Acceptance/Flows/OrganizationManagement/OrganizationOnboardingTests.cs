using Chronos.MainApi.Management.Contracts;
using Chronos.Tests.Acceptance.Infrastructure;
using Chronos.Tests.Acceptance.Support;
using FluentAssertions;

namespace Chronos.Tests.Acceptance.Flows.OrganizationManagement;

/// <summary>
/// Acceptance coverage for the organization-onboarding feature: an administrator
/// registers an organization and can immediately operate within it. Doubles as the
/// proving ground for the <see cref="AcceptanceContext"/> + <see cref="Seeder"/> helpers.
/// </summary>
[TestFixture]
[Category("Acceptance")]
public class OrganizationOnboardingTests
{
    private const string OrgName = "Onboarding Acceptance Org";

    private AcceptanceContext _ctx = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp() => _ctx = await AcceptanceContext.CreateAsync(OrgName);

    [OneTimeTearDown]
    public void OneTimeTearDown() => _ctx.Dispose();

    [Test]
    public async Task GivenRegisteredAdmin_WhenGetOrganizationInfo_ThenReturnsTheirOrganization()
    {
        var response = await _ctx.AdminClient.GetAsync("/api/management/organization/info");
        var info = await response.ReadJsonAsync<OrganizationInformation>();

        info.Should().NotBeNull();
        info!.Name.Should().Be(OrgName);
        info.UserEmail.Should().Be(_ctx.AdminEmail);
        info.Deleted.Should().BeFalse();
    }

    [Test]
    public async Task GivenRegisteredAdmin_WhenCreateDepartment_ThenItAppearsInTheDepartmentList()
    {
        var created = await _ctx.Seed.CreateDepartmentAsync("Engineering");

        var list = await (await _ctx.AdminClient.GetAsync("/api/management/department"))
            .ReadJsonAsync<DepartmentResponse[]>();

        list.Should().NotBeNull();
        list!.Should().ContainSingle(d => d.Id == created.Id)
            .Which.Name.Should().Be("Engineering");
    }
}
