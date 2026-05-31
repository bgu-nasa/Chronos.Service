using System.Net;
using Chronos.MainApi.Auth.Contracts;
using Chronos.Tests.Acceptance.Infrastructure;
using Chronos.Tests.Acceptance.Support;
using FluentAssertions;

namespace Chronos.Tests.Acceptance.Flows.Authentication;

/// <summary>
/// Exercises the public auth flow (register, login, refresh) end-to-end against
/// a fresh API instance. These tests deliberately avoid <see cref="AcceptanceContext"/>
/// because <see cref="AcceptanceContext"/> itself depends on the register flow
/// working; using the raw factory keeps the assertions on the flow under test.
/// </summary>
[TestFixture]
[Category("Acceptance")]
public class AuthFlowTests
{
    private ChronosApiFactory _factory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp() => _factory = new ChronosApiFactory();

    [OneTimeTearDown]
    public void OneTimeTearDown() => _factory.Dispose();

    private static RegisterRequest Register(string email, string orgName, string? password = null) =>
        new(
            AdminUser: new CreateUserRequest(email, "Test", "User", password ?? TestConstants.DefaultPassword),
            OrganizationName: orgName,
            Plan: "free",
            InviteCode: TestConstants.InviteCode);

    [Test]
    public async Task GivenValidRegistration_WhenRegister_ThenReturnsJwtToken()
    {
        var client = _factory.CreateClient();

        var response = await client.PostJsonAsync("/api/auth/register",
            Register("reg-test@chronos.dev", "Auth Test Org"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.ReadJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();
        auth!.Token.Should().NotBeNullOrWhiteSpace();
        auth.Token.Split('.').Should().HaveCount(3, "a JWT has three dot-separated segments");
    }

    [Test]
    public async Task GivenRegisteredUser_WhenLoginWithCorrectCredentials_ThenReturnsJwtToken()
    {
        var client = _factory.CreateClient();
        var email = "login-test@chronos.dev";

        await client.PostJsonAsync("/api/auth/register", Register(email, "Login Test Org"));

        var loginResponse = await client.PostJsonAsync("/api/auth/login",
            new LoginRequest(email, TestConstants.DefaultPassword));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await loginResponse.ReadJsonAsync<AuthResponse>();
        auth!.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task GivenRegisteredUser_WhenLoginWithWrongPassword_ThenReturns401()
    {
        var client = _factory.CreateClient();
        var email = "wrong-pw@chronos.dev";

        await client.PostJsonAsync("/api/auth/register", Register(email, "WrongPw Org"));

        var loginResponse = await client.PostJsonAsync("/api/auth/login",
            new LoginRequest(email, "TotallyWr0ng"));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GivenAuthenticatedUser_WhenRefreshToken_ThenReturnsNewToken()
    {
        var client = _factory.CreateClient();

        var regResponse = await client.PostJsonAsync("/api/auth/register",
            Register("refresh-test@chronos.dev", "Refresh Org"));
        var originalAuth = await regResponse.ReadJsonAsync<AuthResponse>();
        client.UseBearerToken(originalAuth!.Token);

        var refreshResponse = await client.PostAsync("/api/auth/refresh", null);

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshedAuth = await refreshResponse.ReadJsonAsync<AuthResponse>();
        refreshedAuth!.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task GivenInvalidToken_WhenAccessProtectedEndpoint_ThenReturns401()
    {
        var client = _factory.CreateClient();
        client.UseBearerToken("invalid.jwt.token");

        var response = await client.PostAsync("/api/auth/refresh", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
