using System.Net;
using Chronos.Domain.Management.Roles;
using Chronos.Tests.Acceptance.Infrastructure;
using FluentAssertions;

namespace Chronos.Tests.Acceptance.Flows.Health;

[TestFixture]
[Category("Acceptance")]
public class SmokeTests
{
    private ChronosApiFactory _factory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp() => _factory = new ChronosApiFactory();

    [OneTimeTearDown]
    public void OneTimeTearDown() => _factory.Dispose();

    [Test]
    public async Task GivenRunningServer_WhenRequestWithoutAuth_ThenReturns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/department/00000000-0000-0000-0000-000000000000/resources/subjects/Subject");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GivenValidToken_WhenRequestProtectedEndpoint_ThenDoesNotReturn401()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            userId, "test@example.com", orgId,
            new SimpleRoleForToken(Role.Administrator, orgId));

        var response = await client.GetAsync("/api/department/00000000-0000-0000-0000-000000000000/resources/subjects/Subject");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public void GivenFactory_WhenGetDbContext_ThenReturnsUsableContext()
    {
        var (scope, db) = _factory.GetDbContext();
        using (scope)
        {
            db.Should().NotBeNull();
            db.Database.ProviderName.Should().Contain("InMemory");
        }
    }
}
