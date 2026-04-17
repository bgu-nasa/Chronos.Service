using System.Net;
using Chronos.Domain.Management.Roles;

namespace Chronos.Tests.System.Infrastructure;

[TestFixture]
[Category("E2E")]
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

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
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

        Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public void GivenFactory_WhenGetDbContext_ThenReturnsUsableContext()
    {
        var (scope, db) = _factory.GetDbContext();
        using (scope)
        {
            Assert.That(db, Is.Not.Null);
            Assert.That(db.Database.ProviderName, Does.Contain("InMemory"));
        }
    }
}
