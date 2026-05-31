using System.Net;
using Chronos.Domain.Management.Roles;
using Chronos.MainApi.Auth.Contracts;
using Chronos.Tests.Acceptance.Infrastructure;
using Chronos.Tests.Acceptance.Support;
using FluentAssertions;

namespace Chronos.Tests.Acceptance.Flows.Authentication;

/// <summary>
/// Covers the validation and error-path branches of the public surface — duplicate
/// email, weak password, malformed email, missing org header, bad invite code, and
/// 404s for unknown resources. Uses the raw factory rather than <see cref="AcceptanceContext"/>
/// because most cases need to exercise the register flow directly with bad inputs.
/// </summary>
[TestFixture]
[Category("Acceptance")]
public class ErrorHandlingTests
{
    private ChronosApiFactory _factory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp() => _factory = new ChronosApiFactory();

    [OneTimeTearDown]
    public void OneTimeTearDown() => _factory.Dispose();

    private static RegisterRequest Register(string email, string orgName, string? password = null, string? inviteCode = null) =>
        new(
            AdminUser: new CreateUserRequest(email, "Test", "User", password ?? TestConstants.DefaultPassword),
            OrganizationName: orgName,
            Plan: "free",
            InviteCode: inviteCode ?? TestConstants.InviteCode);

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

        await client.PostJsonAsync("/api/auth/register", Register(email, "Dup Org"));

        var secondResponse = await client.PostJsonAsync("/api/auth/register",
            Register(email, "Dup Org 2"));

        secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await secondResponse.Content.ReadAsStringAsync();
        body.Should().Contain("already exists");
    }

    [Test]
    public async Task GivenWeakPassword_WhenRegister_ThenReturns400WithValidationMessage()
    {
        var client = _factory.CreateClient();

        var response = await client.PostJsonAsync("/api/auth/register",
            Register("weak-pw@chronos.dev", "Weak Org", password: "short"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Password must be at least 8 characters long");
    }

    [Test]
    public async Task GivenInvalidEmail_WhenRegister_ThenReturns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostJsonAsync("/api/auth/register",
            Register("not-an-email", "Bad Email Org"));

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
        client.UseBearerToken(token);

        var response = await client.GetAsync("/api/user");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("missing organization id");
    }

    [Test]
    public async Task GivenInvalidInviteCode_WhenRegister_ThenReturns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostJsonAsync("/api/auth/register",
            Register("bad-invite@chronos.dev", "Bad Invite Org", inviteCode: "invalidcode"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("private beta");
    }
}
