using System.Net;
using Chronos.Domain.Management.Roles;
using Chronos.MainApi.Auth.Contracts;
using Chronos.Tests.System.Infrastructure;
using FluentAssertions;

namespace Chronos.Tests.System.Flows;

[TestFixture]
[Category("E2E")]
public class ErrorHandlingTests
{
    private const string ValidInviteCode = "hih";

    private ChronosApiFactory _factory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp() => _factory = new ChronosApiFactory();

    [OneTimeTearDown]
    public void OneTimeTearDown() => _factory.Dispose();

    [Test]
    public async Task GivenNonexistentResourceId_WhenGetById_ThenReturns404()
    {
        var orgId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            Guid.NewGuid(), "err404@test.com", orgId,
            new SimpleRoleForToken(Role.Viewer, orgId));

        var response = await client.GetAsync($"/api/resources/resource/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GivenExistingEmail_WhenRegisterDuplicateEmail_ThenReturns400()
    {
        var client = _factory.CreateClient();
        var email = "duplicate@chronos.dev";
        var request = new RegisterRequest(
            AdminUser: new CreateUserRequest(email, "Dup", "User", "Passw0rd1"),
            OrganizationName: "Dup Org",
            Plan: "free",
            InviteCode: ValidInviteCode);

        await client.PostJsonAsync("/api/auth/register", request);

        var secondResponse = await client.PostJsonAsync("/api/auth/register",
            new RegisterRequest(
                AdminUser: new CreateUserRequest(email, "Dup", "Again", "Passw0rd2"),
                OrganizationName: "Dup Org 2",
                Plan: "free",
                InviteCode: ValidInviteCode));

        secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await secondResponse.Content.ReadAsStringAsync();
        body.Should().Contain("already exists");
    }

    [Test]
    public async Task GivenWeakPassword_WhenRegister_ThenReturns400WithValidationMessage()
    {
        var client = _factory.CreateClient();
        var request = new RegisterRequest(
            AdminUser: new CreateUserRequest("weak-pw@chronos.dev", "Weak", "Pw", "short"),
            OrganizationName: "Weak Org",
            Plan: "free",
            InviteCode: ValidInviteCode);

        var response = await client.PostJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Password must be at least 8 characters long");
    }

    [Test]
    public async Task GivenInvalidEmail_WhenRegister_ThenReturns400()
    {
        var client = _factory.CreateClient();
        var request = new RegisterRequest(
            AdminUser: new CreateUserRequest("not-an-email", "Bad", "Email", "Passw0rd1"),
            OrganizationName: "Bad Email Org",
            Plan: "free",
            InviteCode: ValidInviteCode);

        var response = await client.PostJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Email format is invalid");
    }

    [Test]
    public async Task GivenMissingOrgHeader_WhenAccessScopedEndpoint_ThenReturns400()
    {
        var client = _factory.CreateClient();
        var token = TestTokenGenerator.GenerateToken(
            Guid.NewGuid(), "no-org@test.com", Guid.NewGuid(),
            new SimpleRoleForToken(Role.Administrator, Guid.NewGuid()));
        client.DefaultRequestHeaders.Authorization =
            new global::System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/user");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("missing organization id");
    }

    [Test]
    public async Task GivenInvalidInviteCode_WhenRegister_ThenReturns401()
    {
        var client = _factory.CreateClient();
        var request = new RegisterRequest(
            AdminUser: new CreateUserRequest("bad-invite@chronos.dev", "Bad", "Invite", "Passw0rd1"),
            OrganizationName: "Bad Invite Org",
            Plan: "free",
            InviteCode: "invalidcode");

        var response = await client.PostJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("private beta");
    }
}
