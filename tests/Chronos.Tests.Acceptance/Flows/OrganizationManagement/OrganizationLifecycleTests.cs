using System.Net;
using Chronos.Domain.Management.Roles;
using Chronos.MainApi.Auth.Contracts;
using Chronos.MainApi.Management.Contracts;
using Chronos.Tests.Acceptance.Infrastructure;
using Chronos.Tests.Acceptance.Support;
using FluentAssertions;

namespace Chronos.Tests.Acceptance.Flows.OrganizationManagement;

/// <summary>
/// Walks an organization through its lifecycle (info → soft-delete → restore) and
/// verifies the surrounding admin surface (department CRUD, role assignment).
/// Each test gets a fresh <see cref="AcceptanceContext"/> so mutations (soft-delete,
/// department creation) don't leak between cases.
/// </summary>
[TestFixture]
[Category("Acceptance")]
public class OrganizationLifecycleTests
{
    [Test]
    public async Task GivenRegisteredOrganization_WhenGetOrgInfo_ThenReturnsFullDetails()
    {
        using var ctx = await AcceptanceContext.CreateAsync("Info Test Org");

        var info = await (await ctx.AdminClient.GetAsync("/api/management/organization/info"))
            .ReadJsonAsync<OrganizationInformation>();

        info.Should().NotBeNull();
        info!.Name.Should().Be("Info Test Org");
        info.UserEmail.Should().Be(ctx.AdminEmail);
        info.Deleted.Should().BeFalse();
    }

    [Test]
    public async Task GivenExistingOrganization_WhenSoftDeleteAndRestore_ThenLifecycleCompletes()
    {
        using var ctx = await AcceptanceContext.CreateAsync();

        var deleteResponse = await ctx.AdminClient.DeleteAsync("/api/management/organization");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var restoreResponse = await ctx.AdminClient.PostAsync("/api/management/organization/restore", null);
        restoreResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var infoAfterRestore = await (await ctx.AdminClient.GetAsync("/api/management/organization/info"))
            .ReadJsonAsync<OrganizationInformation>();
        infoAfterRestore!.Deleted.Should().BeFalse();
    }

    [Test]
    public async Task GivenExistingOrganization_WhenCreateDepartment_ThenCreatesDepartment()
    {
        using var ctx = await AcceptanceContext.CreateAsync();

        var dept = await ctx.Seed.CreateDepartmentAsync("Computer Science");

        dept.Name.Should().Be("Computer Science");
        dept.Deleted.Should().BeFalse();
    }

    [Test]
    public async Task GivenExistingDepartment_WhenGetDepartments_ThenIncludesCreatedDepartment()
    {
        using var ctx = await AcceptanceContext.CreateAsync();

        await ctx.Seed.CreateDepartmentAsync("Mathematics");
        await ctx.Seed.CreateDepartmentAsync("Physics");

        var departments = await (await ctx.AdminClient.GetAsync("/api/management/department"))
            .ReadJsonAsync<DepartmentResponse[]>();

        departments.Should().NotBeNull();
        departments!.Select(d => d.Name).Should().Contain(["Mathematics", "Physics"]);
    }

    [Test]
    public async Task GivenRegisteredOrganization_WhenCreateAndAssignRole_ThenRoleIsRetrievable()
    {
        using var ctx = await AcceptanceContext.CreateAsync();

        var createdUser = await ctx.Seed.CreateUserAsync("role-target@chronos.dev");

        var roleResponse = await ctx.AdminClient.PostJsonAsync("/api/management/role",
            new RoleAssignmentRequest(null, Guid.Parse(createdUser.UserId), RoleType.Viewer));

        roleResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var rolesResponse = await ctx.AdminClient.GetAsync(
            $"/api/management/role/user/{createdUser.UserId}");
        rolesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task GivenAlreadyDeletedOrganization_WhenSoftDeleteAgain_ThenReturns400()
    {
        using var ctx = await AcceptanceContext.CreateAsync();

        var firstDelete = await ctx.AdminClient.DeleteAsync("/api/management/organization");
        firstDelete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secondDelete = await ctx.AdminClient.DeleteAsync("/api/management/organization");
        secondDelete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GivenActiveOrganization_WhenRestoreWithoutDeletion_ThenReturns400()
    {
        using var ctx = await AcceptanceContext.CreateAsync();

        var restoreResponse = await ctx.AdminClient.PostAsync("/api/management/organization/restore", null);
        restoreResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GivenExistingDepartment_WhenSoftDelete_ThenDepartmentIsMarkedDeleted()
    {
        using var ctx = await AcceptanceContext.CreateAsync();
        var dept = await ctx.Seed.CreateDepartmentAsync($"Dept to delete {Guid.NewGuid():N}");

        var deleteResponse = await ctx.AdminClient.DeleteAsync($"/api/management/department/{dept.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await ctx.AdminClient.GetAsync($"/api/management/department/{dept.Id}");
        var updated = await getResponse.ReadJsonAsync<DepartmentResponse>();
        updated!.Deleted.Should().BeTrue();
    }

    [Test]
    public async Task GivenAlreadyDeletedDepartment_WhenSoftDeleteAgain_ThenReturns400()
    {
        using var ctx = await AcceptanceContext.CreateAsync();
        var dept = await ctx.Seed.CreateDepartmentAsync($"Dept double delete {Guid.NewGuid():N}");

        await ctx.AdminClient.DeleteAsync($"/api/management/department/{dept.Id}");

        var secondDelete = await ctx.AdminClient.DeleteAsync($"/api/management/department/{dept.Id}");
        secondDelete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GivenSoftDeletedDepartment_WhenRestore_ThenDepartmentIsActive()
    {
        using var ctx = await AcceptanceContext.CreateAsync();
        var dept = await ctx.Seed.CreateDepartmentAsync($"Dept to restore {Guid.NewGuid():N}");

        await ctx.AdminClient.DeleteAsync($"/api/management/department/{dept.Id}");

        var restoreResponse = await ctx.AdminClient.PostAsync($"/api/management/department/restore/{dept.Id}", null);
        restoreResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await ctx.AdminClient.GetAsync($"/api/management/department/{dept.Id}");
        var restored = await getResponse.ReadJsonAsync<DepartmentResponse>();
        restored!.Deleted.Should().BeFalse();
    }

    [Test]
    public async Task GivenActiveDepartment_WhenRestoreWithoutDeletion_ThenReturns400()
    {
        using var ctx = await AcceptanceContext.CreateAsync();
        var dept = await ctx.Seed.CreateDepartmentAsync($"Dept restore error {Guid.NewGuid():N}");

        var restoreResponse = await ctx.AdminClient.PostAsync($"/api/management/department/restore/{dept.Id}", null);
        restoreResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
