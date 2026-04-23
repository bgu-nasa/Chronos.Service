using System.Net;
using Chronos.Domain.Management;
using Chronos.Domain.Management.Roles;
using Chronos.MainApi.Auth.Contracts;
using Chronos.MainApi.Management.Contracts;
using Chronos.Tests.System.Infrastructure;
using FluentAssertions;

namespace Chronos.Tests.System.Flows;

[TestFixture]
[Category("E2E")]
public class OrganizationLifecycleTests
{
    private const string ValidInviteCode = "hih";

    private ChronosApiFactory _factory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp() => _factory = new ChronosApiFactory();

    [OneTimeTearDown]
    public void OneTimeTearDown() => _factory.Dispose();

    [Test]
    public async Task GivenRegisteredOrganization_WhenGetOrgInfo_ThenReturnsFullDetails()
    {
        var client = _factory.CreateClient();
        var regResponse = await client.PostJsonAsync("/api/auth/register",
            new RegisterRequest(
                AdminUser: new CreateUserRequest("org-info@chronos.dev", "Org", "Admin", "Passw0rd1"),
                OrganizationName: "Info Test Org",
                Plan: "free",
                InviteCode: ValidInviteCode));

        var auth = await regResponse.ReadJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new global::System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.Token);

        var infoResponse = await client.GetAsync("/api/management/organization/info");

        infoResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var info = await infoResponse.ReadJsonAsync<OrganizationInformation>();
        info.Should().NotBeNull();
        info!.Name.Should().Be("Info Test Org");
        info.UserEmail.Should().Be("org-info@chronos.dev");
        info.Deleted.Should().BeFalse();
    }

    [Test]
    public async Task GivenExistingOrganization_WhenSoftDeleteAndRestore_ThenLifecycleCompletes()
    {
        var client = _factory.CreateClient();
        var regResponse = await client.PostJsonAsync("/api/auth/register",
            new RegisterRequest(
                AdminUser: new CreateUserRequest("lifecycle@chronos.dev", "Life", "Cycle", "Passw0rd1"),
                OrganizationName: "Lifecycle Org",
                Plan: "free",
                InviteCode: ValidInviteCode));

        var auth = await regResponse.ReadJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new global::System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.Token);

        var orgInfo = await (await client.GetAsync("/api/management/organization/info"))
            .ReadJsonAsync<OrganizationInformation>();
        client.DefaultRequestHeaders.Add("x-org-id", orgInfo!.Id.ToString());

        // Soft-delete the organization
        var deleteResponse = await client.DeleteAsync("/api/management/organization");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Restore the organization
        var restoreResponse = await client.PostAsync("/api/management/organization/restore", null);
        restoreResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's restored by fetching info again
        var infoAfterRestore = await (await client.GetAsync("/api/management/organization/info"))
            .ReadJsonAsync<OrganizationInformation>();
        infoAfterRestore!.Deleted.Should().BeFalse();
    }

    [Test]
    public async Task GivenExistingOrganization_WhenCreateDepartment_ThenReturns201()
    {
        var client = _factory.CreateClient();
        var regResponse = await client.PostJsonAsync("/api/auth/register",
            new RegisterRequest(
                AdminUser: new CreateUserRequest("dept-create@chronos.dev", "Dept", "Creator", "Passw0rd1"),
                OrganizationName: "Dept Create Org",
                Plan: "free",
                InviteCode: ValidInviteCode));

        var auth = await regResponse.ReadJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new global::System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.Token);

        var orgInfo = await (await client.GetAsync("/api/management/organization/info"))
            .ReadJsonAsync<OrganizationInformation>();
        client.DefaultRequestHeaders.Add("x-org-id", orgInfo!.Id.ToString());

        var createResponse = await client.PostJsonAsync("/api/management/department",
            new DepartmentRequest("Computer Science"));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var dept = await createResponse.ReadJsonAsync<DepartmentResponse>();
        dept!.Name.Should().Be("Computer Science");
        dept.Deleted.Should().BeFalse();
    }

    [Test]
    public async Task GivenExistingDepartment_WhenGetDepartments_ThenIncludesCreatedDepartment()
    {
        var client = _factory.CreateClient();
        var regResponse = await client.PostJsonAsync("/api/auth/register",
            new RegisterRequest(
                AdminUser: new CreateUserRequest("dept-list@chronos.dev", "Dept", "Lister", "Passw0rd1"),
                OrganizationName: "Dept List Org",
                Plan: "free",
                InviteCode: ValidInviteCode));

        var auth = await regResponse.ReadJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new global::System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.Token);

        var orgInfo = await (await client.GetAsync("/api/management/organization/info"))
            .ReadJsonAsync<OrganizationInformation>();
        client.DefaultRequestHeaders.Add("x-org-id", orgInfo!.Id.ToString());

        await client.PostJsonAsync("/api/management/department", new DepartmentRequest("Mathematics"));
        await client.PostJsonAsync("/api/management/department", new DepartmentRequest("Physics"));

        var listResponse = await client.GetAsync("/api/management/department");

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var departments = await listResponse.ReadJsonAsync<DepartmentResponse[]>();
        departments.Should().NotBeNull();
        departments!.Length.Should().BeGreaterThanOrEqualTo(2);
        departments.Select(d => d.Name).Should().Contain("Mathematics").And.Contain("Physics");
    }

    [Test]
    public async Task GivenRegisteredOrganization_WhenCreateAndAssignRole_ThenRoleIsRetrievable()
    {
        var client = _factory.CreateClient();
        var regResponse = await client.PostJsonAsync("/api/auth/register",
            new RegisterRequest(
                AdminUser: new CreateUserRequest("role-test@chronos.dev", "Role", "Tester", "Passw0rd1"),
                OrganizationName: "Role Test Org",
                Plan: "free",
                InviteCode: ValidInviteCode));

        var auth = await regResponse.ReadJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new global::System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.Token);

        var orgInfo = await (await client.GetAsync("/api/management/organization/info"))
            .ReadJsonAsync<OrganizationInformation>();
        client.DefaultRequestHeaders.Add("x-org-id", orgInfo!.Id.ToString());

        // Create a second user to assign a role to
        var createUserResponse = await client.PostJsonAsync("/api/user",
            new CreateUserRequest("role-target@chronos.dev", "Target", "User", "Passw0rd2"));
        createUserResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdUser = await createUserResponse.ReadJsonAsync<CreateUserResponse>();

        var roleResponse = await client.PostJsonAsync("/api/management/role",
            new RoleAssignmentRequest(null, Guid.Parse(createdUser!.UserId), RoleType.Viewer));

        roleResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var rolesResponse = await client.GetAsync(
            $"/api/management/role/user/{createdUser.UserId}");
        rolesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
