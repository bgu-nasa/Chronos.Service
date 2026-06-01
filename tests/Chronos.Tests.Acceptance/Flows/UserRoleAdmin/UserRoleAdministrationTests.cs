using System.Net;
using Chronos.MainApi.Auth.Contracts;
using Chronos.MainApi.Management.Contracts;
using Chronos.Tests.Acceptance.Infrastructure;
using Chronos.Tests.Acceptance.Support;
using FluentAssertions;

namespace Chronos.Tests.Acceptance.Flows.UserRoleAdmin;

[TestFixture]
[Category("Acceptance")]
public class UserRoleAdministrationTests
{
    private AcceptanceContext _ctx = null!;

    [SetUp]
    public async Task SetUp()
    {
        _ctx = await AcceptanceContext.CreateAsync("User Role Admin Acceptance Org");
    }

    [TearDown]
    public void TearDown()
    {
        _ctx.Dispose();
    }

    [Test]
    public async Task GivenNewUser_WhenAdminGrantsAndRevokesDepartmentRole_ThenRoleViewsReflectCurrentAccess()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = await _ctx.Seed.CreateUserAsync(
            $"role-target-{suffix}@chronos.test",
            firstName: "Role",
            lastName: "Target");
        var userId = Guid.Parse(user.UserId);
        var department = await _ctx.Seed.CreateDepartmentAsync($"Access Dept {suffix}");

        var userProfile = await ReadUserAsync(userId);
        userProfile.Email.Should().Be(user.Email);
        userProfile.FirstName.Should().Be("Role");
        userProfile.LastName.Should().Be("Target");

        var updateProfile = await _ctx.AdminClient.PutJsonAsync($"/api/user/{userId}",
            new UserUpdateRequest("Updated", "Target", "https://example.com/avatar.png"));
        updateProfile.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var updatedProfile = await ReadUserAsync(userId);
        updatedProfile.FirstName.Should().Be("Updated");
        updatedProfile.AvatarUrl.Should().Be("https://example.com/avatar.png");

        var assignRole = await _ctx.AdminClient.PostJsonAsync("/api/management/role",
            new RoleAssignmentRequest(department.Id, userId, RoleType.ResourceManager));
        assignRole.StatusCode.Should().Be(HttpStatusCode.Created);

        var roleAssignment = await assignRole.ReadJsonAsync<RoleAssignmentResponse>();
        roleAssignment.Should().NotBeNull();
        roleAssignment!.UserId.Should().Be(userId);
        roleAssignment.OrganizationId.Should().Be(_ctx.OrganizationId);
        roleAssignment.DepartmentId.Should().Be(department.Id);
        roleAssignment.Role.Should().Be(RoleType.ResourceManager);

        var byId = await ReadRoleAssignmentAsync(roleAssignment.Id);
        byId.Should().BeEquivalentTo(roleAssignment);

        var rolesForUser = await ReadRoleAssignmentsAsync($"/api/management/role/user/{userId}");
        rolesForUser.Should().ContainSingle(r => r.Id == roleAssignment.Id);

        var allRoles = await ReadRoleAssignmentsAsync("/api/management/role");
        allRoles.Should().Contain(r => r.Id == roleAssignment.Id);

        var summary = await ReadRoleSummaryAsync();
        summary.Should().ContainSingle(s =>
            s.UserEmail == user.Email &&
            s.Assignments.Any(a => a.Id == roleAssignment.Id));

        var revoke = await _ctx.AdminClient.DeleteAsync($"/api/management/role/{roleAssignment.Id}");
        revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var rolesAfterRevoke = await ReadRoleAssignmentsAsync($"/api/management/role/user/{userId}");
        rolesAfterRevoke.Should().NotContain(r => r.Id == roleAssignment.Id);

        var deleteUser = await _ctx.AdminClient.DeleteAsync($"/api/user/{userId}");
        deleteUser.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await _ctx.AdminClient.GetAsync($"/api/user/{userId}");
        afterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GivenMissingDepartment_WhenAdminAssignsDepartmentRole_ThenRequestIsRejected()
    {
        var user = await _ctx.Seed.CreateUserAsync($"missing-dept-target-{Guid.NewGuid():N}@chronos.test");

        var response = await _ctx.AdminClient.PostJsonAsync("/api/management/role",
            new RoleAssignmentRequest(Guid.NewGuid(), Guid.Parse(user.UserId), RoleType.Operator));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GivenSystemAssignedAdminRole_WhenAdminTriesToRevokeIt_ThenRequestIsRejected()
    {
        var adminRoles = await ReadRoleAssignmentsAsync($"/api/management/role/user/{_ctx.AdminUserId}");
        var adminRole = adminRoles.Single(r => r.Role == RoleType.Administrator && r.DepartmentId is null);

        var response = await _ctx.AdminClient.DeleteAsync($"/api/management/role/{adminRole.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var rolesAfterAttempt = await ReadRoleAssignmentsAsync($"/api/management/role/user/{_ctx.AdminUserId}");
        rolesAfterAttempt.Should().ContainSingle(r => r.Id == adminRole.Id);
    }

    private async Task<UserResponse> ReadUserAsync(Guid userId)
    {
        var response = await _ctx.AdminClient.GetAsync($"/api/user/{userId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ReadJsonAsync<UserResponse>()
               ?? throw new InvalidOperationException($"GET /api/user/{userId} returned no user.");
    }

    private async Task<RoleAssignmentResponse> ReadRoleAssignmentAsync(Guid roleAssignmentId)
    {
        var response = await _ctx.AdminClient.GetAsync($"/api/management/role/{roleAssignmentId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ReadJsonAsync<RoleAssignmentResponse>()
               ?? throw new InvalidOperationException($"GET /api/management/role/{roleAssignmentId} returned no role.");
    }

    private async Task<RoleAssignmentResponse[]> ReadRoleAssignmentsAsync(string url)
    {
        var response = await _ctx.AdminClient.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ReadJsonAsync<RoleAssignmentResponse[]>()
               ?? throw new InvalidOperationException($"GET {url} returned no roles.");
    }

    private async Task<UserRoleAssignmentSummary[]> ReadRoleSummaryAsync()
    {
        var response = await _ctx.AdminClient.GetAsync("/api/management/role/summary");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ReadJsonAsync<UserRoleAssignmentSummary[]>()
               ?? throw new InvalidOperationException("GET /api/management/role/summary returned no summary.");
    }
}
