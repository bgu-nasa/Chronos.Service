using System.Net;
using Chronos.Data.Context;
using Chronos.Domain.Management;
using Chronos.Domain.Management.Roles;
using Chronos.Domain.Resources;
using Chronos.MainApi.Resources.Contracts;
using Chronos.MainApi.Schedule.Contracts;
using Chronos.Tests.System.Infrastructure;
using FluentAssertions;

namespace Chronos.Tests.System.Flows;

[TestFixture]
[Category("E2E")]
public class RbacEnforcementTests
{
    private ChronosApiFactory _factory = null!;
    private Guid _orgId;
    private Guid _deptId;
    private Guid _userId;
    private Guid _resourceTypeId;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new ChronosApiFactory();
        _orgId = Guid.NewGuid();
        _deptId = Guid.NewGuid();
        _userId = Guid.NewGuid();
        _resourceTypeId = Guid.NewGuid();

        // Force the host to start, then seed data
        _ = _factory.CreateClient();

        await SeedBaseData();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown() => _factory.Dispose();

    [Test]
    public async Task GivenViewerRole_WhenGetResources_ThenReturns200()
    {
        var client = _factory.CreateAuthenticatedClient(
            _userId, "viewer@rbac.dev", _orgId,
            new SimpleRoleForToken(Role.Viewer, _orgId));

        var response = await client.GetAsync("/api/resources/resource");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task GivenUnauthenticatedClient_WhenCreateResource_ThenReturns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostJsonAsync("/api/resources/resource",
            new CreateResourceRequest(
                Guid.NewGuid(), _orgId, _resourceTypeId, "B1-101", "Room-V", 30));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GivenResourceManagerRole_WhenCreateResourceType_ThenReturns201()
    {
        var client = _factory.CreateAuthenticatedClient(
            _userId, "manager@rbac.dev", _orgId,
            new SimpleRoleForToken(Role.ResourceManager, _orgId));

        var response = await client.PostJsonAsync("/api/resources/resource/types",
            new CreateResourceTypeRequest(Guid.NewGuid(), _orgId, "Lab"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync<ResourceTypeResponse>();
        body!.Type.Should().Be("Lab");
    }

    [Test]
    public async Task GivenOperatorRole_WhenCreateActivityConstraint_ThenReturns201()
    {
        var activityId = await SeedActivityForConstraintTest();

        var client = _factory.CreateAuthenticatedClient(
            _userId, "operator@rbac.dev", _orgId,
            new SimpleRoleForToken(Role.Operator, _orgId));

        var response = await client.PostJsonAsync("/api/schedule/constraints/activityConstraint",
            new CreateActivityConstraintRequest(activityId, "maxStudents", "50"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Test]
    public async Task GivenAuthenticatedUser_WhenDeleteNonexistentResource_ThenReturns404()
    {
        var client = _factory.CreateAuthenticatedClient(
            _userId, "manager@rbac.dev", _orgId,
            new SimpleRoleForToken(Role.ResourceManager, _orgId));

        var response = await client.DeleteAsync($"/api/resources/resource/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GivenDifferentOrg_WhenAccessOtherOrgResource_ThenResourceNotVisible()
    {
        var managerClient = _factory.CreateAuthenticatedClient(
            _userId, "manager@rbac.dev", _orgId,
            new SimpleRoleForToken(Role.ResourceManager, _orgId));

        var resourceId = Guid.NewGuid();
        await managerClient.PostJsonAsync("/api/resources/resource",
            new CreateResourceRequest(resourceId, _orgId, _resourceTypeId, "B2-200", "Room-Orig", 40));

        var otherOrgId = Guid.NewGuid();
        var otherClient = _factory.CreateAuthenticatedClient(
            Guid.NewGuid(), "other@rbac.dev", otherOrgId,
            new SimpleRoleForToken(Role.Viewer, otherOrgId));

        var response = await otherClient.GetAsync($"/api/resources/resource/{resourceId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task SeedBaseData()
    {
        var (scope, db) = _factory.GetDbContext();
        using (scope)
        {
            db.Set<Organization>().Add(new Organization
            {
                Id = _orgId,
                Name = "RBAC Test Org",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            db.Set<Department>().Add(new Department
            {
                Id = _deptId,
                OrganizationId = _orgId,
                Name = "RBAC Dept",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            db.Set<ResourceType>().Add(new ResourceType
            {
                Id = _resourceTypeId,
                OrganizationId = _orgId,
                Type = "Classroom",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }
    }

    private async Task<Guid> SeedActivityForConstraintTest()
    {
        var periodId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        var (scope, db) = _factory.GetDbContext();
        using (scope)
        {
            db.Set<Chronos.Domain.Schedule.SchedulingPeriod>().Add(
                new Chronos.Domain.Schedule.SchedulingPeriod
                {
                    Id = periodId,
                    OrganizationId = _orgId,
                    Name = "Constraint Period",
                    FromDate = DateTime.UtcNow,
                    ToDate = DateTime.UtcNow.AddMonths(3),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

            db.Set<Subject>().Add(new Subject
            {
                Id = subjectId,
                OrganizationId = _orgId,
                DepartmentId = _deptId,
                SchedulingPeriodId = periodId,
                Code = "CS101",
                Name = "Intro to CS",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            db.Set<Activity>().Add(new Activity
            {
                Id = activityId,
                OrganizationId = _orgId,
                SubjectId = subjectId,
                AssignedUserId = _userId,
                ActivityType = "Lecture",
                Duration = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        return activityId;
    }
}
