using System.Net;
using System.Net.Http.Json;
using Chronos.MainApi.Auth.Contracts;
using Chronos.Tests.System.Infrastructure;
using FluentAssertions;

namespace Chronos.Tests.System.Flows;

[TestFixture]
[Category("E2E")]
public class AuthFlowTests
{
    private const string ValidInviteCode = "hih";

    private ChronosApiFactory _factory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp() => _factory = new ChronosApiFactory();

    [OneTimeTearDown]
    public void OneTimeTearDown() => _factory.Dispose();

    [Test]
    public async Task GivenValidRegistration_WhenRegister_ThenReturnsJwtToken()
    {
        var client = _factory.CreateClient();
        var request = new RegisterRequest(
            AdminUser: new CreateUserRequest("reg-test@chronos.dev", "Admin", "User", "Passw0rd1"),
            OrganizationName: "Auth Test Org",
            Plan: "free",
            InviteCode: ValidInviteCode);

        var response = await client.PostJsonAsync("/api/auth/register", request);

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
        var password = "Passw0rd2";

        await client.PostJsonAsync("/api/auth/register", new RegisterRequest(
            AdminUser: new CreateUserRequest(email, "Login", "Tester", password),
            OrganizationName: "Login Test Org",
            Plan: "free",
            InviteCode: ValidInviteCode));

        var loginResponse = await client.PostJsonAsync("/api/auth/login",
            new LoginRequest(email, password));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await loginResponse.ReadJsonAsync<AuthResponse>();
        auth!.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task GivenRegisteredUser_WhenLoginWithWrongPassword_ThenReturns401()
    {
        var client = _factory.CreateClient();
        var email = "wrong-pw@chronos.dev";

        await client.PostJsonAsync("/api/auth/register", new RegisterRequest(
            AdminUser: new CreateUserRequest(email, "Wrong", "Pw", "Passw0rd3"),
            OrganizationName: "WrongPw Org",
            Plan: "free",
            InviteCode: ValidInviteCode));

        var loginResponse = await client.PostJsonAsync("/api/auth/login",
            new LoginRequest(email, "TotallyWr0ng"));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GivenAuthenticatedUser_WhenRefreshToken_ThenReturnsNewToken()
    {
        var client = _factory.CreateClient();
        var email = "refresh-test@chronos.dev";

        var regResponse = await client.PostJsonAsync("/api/auth/register", new RegisterRequest(
            AdminUser: new CreateUserRequest(email, "Refresh", "Tester", "Passw0rd4"),
            OrganizationName: "Refresh Org",
            Plan: "free",
            InviteCode: ValidInviteCode));

        var originalAuth = await regResponse.ReadJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new global::System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", originalAuth!.Token);

        var refreshResponse = await client.PostAsync("/api/auth/refresh", null);

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshedAuth = await refreshResponse.ReadJsonAsync<AuthResponse>();
        refreshedAuth!.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task GivenInvalidToken_WhenAccessProtectedEndpoint_ThenReturns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new global::System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.jwt.token");

        var response = await client.PostAsync("/api/auth/refresh", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
