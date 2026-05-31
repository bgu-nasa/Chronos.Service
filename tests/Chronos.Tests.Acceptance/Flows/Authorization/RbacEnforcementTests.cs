using System.Net;
using Chronos.Domain.Management.Roles;
using Chronos.MainApi.Resources.Contracts;
using Chronos.MainApi.Schedule.Contracts;
using Chronos.Tests.Acceptance.Infrastructure;
using Chronos.Tests.Acceptance.Support;
using FluentAssertions;

namespace Chronos.Tests.Acceptance.Flows.Authorization;

/// <summary>
/// Spot-checks RBAC policy enforcement on representative endpoints: that role-bearing
/// tokens get through, unauthenticated requests are rejected, and a resource owned by
/// one organization is invisible from another. Builds prerequisite domain data through
/// the public API (<see cref="Seeder"/>) so the tests exercise the same authorization
/// chain users hit in production.
/// </summary>
[TestFixture]
[Category("Acceptance")]
public class RbacEnforcementTests
{
    private AcceptanceContext _ctx = null!;
    private Guid _resourceTypeId;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _ctx = await AcceptanceContext.CreateAsync("RBAC Test Org");

        var resourceType = await _ctx.Seed.CreateResourceTypeAsync(_ctx.OrganizationId, "Classroom");
        _resourceTypeId = resourceType.Id;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown() => _ctx.Dispose();

    [Test]
    public async Task GivenViewerRole_WhenGetResources_ThenReturns200()
    {
        var client = _ctx.CreateClientAs(Role.Viewer);

        var response = await client.GetAsync("/api/resources/resource");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task GivenUnauthenticatedClient_WhenCreateResource_ThenReturns401()
    {
        var client = _ctx.Factory.CreateClient();

        var response = await client.PostJsonAsync("/api/resources/resource",
            new CreateResourceRequest(
                _ctx.OrganizationId, _resourceTypeId, "B1-101", "Room-V", 30));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GivenResourceManagerRole_WhenCreateResourceType_ThenReturns201()
    {
        var client = _ctx.CreateClientAs(Role.ResourceManager);

        var response = await client.PostJsonAsync("/api/resources/resource/types",
            new CreateResourceTypeRequest(_ctx.OrganizationId, "Lab"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync<ResourceTypeResponse>();
        body!.Type.Should().Be("Lab");
    }

    [Test]
    public async Task GivenOperatorRole_WhenCreateActivityConstraint_ThenReturns201()
    {
        var activity = await _ctx.Seed.CreateActivityWithPrerequisitesAsync(_ctx.OrganizationId);

        var client = _ctx.CreateClientAs(Role.Operator);

        var response = await client.PostJsonAsync("/api/schedule/constraints/activityConstraint",
            new CreateActivityConstraintRequest(activity.Id, "maxStudents", "50"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Test]
    public async Task GivenAuthenticatedUser_WhenDeleteNonexistentResource_ThenReturns404()
    {
        var client = _ctx.CreateClientAs(Role.ResourceManager);

        var response = await client.DeleteAsync($"/api/resources/resource/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GivenDifferentOrg_WhenAccessOtherOrgResource_ThenReturns404()
    {
        var createdResource = await _ctx.Seed.CreateResourceAsync(
            _ctx.OrganizationId, _resourceTypeId, "B2-200", "Room-Orig", 40);

        using var otherCtx = await AcceptanceContext.CreateAsync(_ctx.Factory, "Other RBAC Org");

        var response = await otherCtx.AdminClient.GetAsync(
            $"/api/resources/resource/{createdResource.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a resource owned by another organization must be invisible across the tenant boundary");
    }
}
