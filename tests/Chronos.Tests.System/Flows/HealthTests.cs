using System.Net;
using Chronos.Domain.Management.Roles;
using Chronos.Tests.System.Infrastructure;
using FluentAssertions;

namespace Chronos.Tests.System.Flows;

[TestFixture]
[Category("E2E")]
public class HealthTests
{
    private ChronosApiFactory _factory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp() => _factory = new ChronosApiFactory();

    [OneTimeTearDown]
    public void OneTimeTearDown() => _factory.Dispose();

    [Test]
    public async Task GivenAnonymousClient_WhenGetHealth_ThenReturns200WithServiceInfo()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Service is healthy");
    }

    [Test]
    public async Task GivenAnonymousClient_WhenGetAuthorizedHealth_ThenReturns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health/test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GivenAuthenticatedClient_WhenGetAuthorizedHealth_ThenReturns200()
    {
        var orgId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            Guid.NewGuid(), "health@test.com", orgId,
            new SimpleRoleForToken(Role.Viewer, orgId));

        var response = await client.GetAsync("/api/health/test");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Authorized service is healthy");
    }
}
