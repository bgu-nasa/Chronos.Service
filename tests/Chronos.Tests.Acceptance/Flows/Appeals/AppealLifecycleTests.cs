using System.Net;
using Chronos.MainApi.Schedule.Contracts;
using Chronos.Tests.Acceptance.Infrastructure;
using Chronos.Tests.Acceptance.Support;
using FluentAssertions;

namespace Chronos.Tests.Acceptance.Flows.Appeals;

[TestFixture]
[Category("Acceptance")]
public class AppealLifecycleTests
{
    private AcceptanceContext _ctx = null!;

    [SetUp]
    public async Task SetUp()
    {
        _ctx = await AcceptanceContext.CreateAsync("Appeals Acceptance Org");
    }

    [TearDown]
    public void TearDown()
    {
        _ctx.Dispose();
    }

    [Test]
    public async Task GivenAssignment_WhenAppealIsSubmitted_ThenItIsVisibleForTheAssignmentAndAssignedUser()
    {
        var setup = await CreateAssignedActivityAsync();

        var create = await _ctx.AdminClient.PostJsonAsync("/api/schedule/scheduling/appeals",
            new CreateAppealRequest(setup.AssignmentId, "Room conflict", "The assigned room is too small."));
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await create.ReadJsonAsync<AppealResponse>();
        created.Should().NotBeNull();
        created!.AssignmentId.Should().Be(setup.AssignmentId.ToString());
        created.OrganizationId.Should().Be(_ctx.OrganizationId.ToString());
        created.Title.Should().Be("Room conflict");
        created.Description.Should().Be("The assigned room is too small.");
        created.CreatedAt.Should().NotBe(default);
        created.UpdatedAt.Should().NotBe(default);

        var appealId = Guid.Parse(created.Id);

        var byId = await ReadAppealAsync($"/api/schedule/scheduling/appeals/{appealId}");
        byId.Should().BeEquivalentTo(created);

        var allAppeals = await ReadAppealsAsync("/api/schedule/scheduling/appeals");
        allAppeals.Should().ContainSingle(a => a.Id == created.Id);

        var byAssignment = await ReadAppealsAsync($"/api/schedule/scheduling/assignments/{setup.AssignmentId}/appeals");
        byAssignment.Should().ContainSingle(a => a.Id == created.Id);

        var byUser = await ReadAppealsAsync($"/api/schedule/scheduling/users/{setup.AssignedUserId}/appeals");
        byUser.Should().ContainSingle(a => a.Id == created.Id);
    }

    [Test]
    public async Task GivenAssignmentWithExistingAppeal_WhenAnotherAppealIsSubmitted_ThenDuplicateIsRejectedAndOriginalRemains()
    {
        var setup = await CreateAssignedActivityAsync();

        var first = await _ctx.AdminClient.PostJsonAsync("/api/schedule/scheduling/appeals",
            new CreateAppealRequest(setup.AssignmentId, "Initial appeal", "First explanation."));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var original = await first.ReadJsonAsync<AppealResponse>();
        original.Should().NotBeNull();

        var duplicate = await _ctx.AdminClient.PostJsonAsync("/api/schedule/scheduling/appeals",
            new CreateAppealRequest(setup.AssignmentId, "Duplicate appeal", "Second explanation."));

        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var byAssignment = await ReadAppealsAsync($"/api/schedule/scheduling/assignments/{setup.AssignmentId}/appeals");
        byAssignment.Should().ContainSingle();
        byAssignment[0].Id.Should().Be(original!.Id);
        byAssignment[0].Title.Should().Be("Initial appeal");
        byAssignment[0].Description.Should().Be("First explanation.");
    }

    [Test]
    public async Task GivenMissingAssignment_WhenAppealIsSubmitted_ThenRequestIsRejected()
    {
        var missingAssignmentId = Guid.NewGuid();

        var response = await _ctx.AdminClient.PostJsonAsync("/api/schedule/scheduling/appeals",
            new CreateAppealRequest(missingAssignmentId, "Cannot attend", "The referenced assignment does not exist."));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<AssignedActivity> CreateAssignedActivityAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var department = await _ctx.Seed.CreateDepartmentAsync($"Appeals Dept {suffix}");
        var period = await _ctx.Seed.CreateSchedulingPeriodAsync(
            $"Appeals Period {suffix}",
            DateTime.UtcNow.Date.AddDays(30),
            DateTime.UtcNow.Date.AddDays(120));
        var slot = await _ctx.Seed.CreateSlotAsync(
            Guid.Parse(period.Id),
            WeekDays.Monday,
            TimeSpan.FromHours(9),
            TimeSpan.FromHours(11));
        var resourceType = await _ctx.Seed.CreateResourceTypeAsync(_ctx.OrganizationId, $"Appeals Room {suffix}");
        var resource = await _ctx.Seed.CreateResourceAsync(
            _ctx.OrganizationId,
            resourceType.Id,
            "Appeals Building",
            $"Room {suffix}",
            capacity: 80);
        var user = await _ctx.Seed.CreateUserAsync($"appeals-instructor-{suffix}@chronos.test");
        var subject = await _ctx.Seed.CreateSubjectAsync(
            _ctx.OrganizationId,
            department.Id,
            Guid.Parse(period.Id),
            $"APL{suffix}",
            $"Appeals Subject {suffix}");
        var activity = await _ctx.Seed.CreateActivityAsync(
            _ctx.OrganizationId,
            department.Id,
            subject.Id,
            Guid.Parse(user.UserId),
            "Lecture",
            expectedStudents: 35,
            duration: 2);
        var assignment = await _ctx.Seed.CreateAssignmentAsync(
            Guid.Parse(slot.Id),
            resource.Id,
            activity.Id,
            weekNum: 1);

        return new AssignedActivity(
            Guid.Parse(assignment.Id),
            Guid.Parse(user.UserId));
    }

    private async Task<AppealResponse> ReadAppealAsync(string url)
    {
        var response = await _ctx.AdminClient.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ReadJsonAsync<AppealResponse>()
               ?? throw new InvalidOperationException($"GET {url} returned no appeal.");
    }

    private async Task<AppealResponse[]> ReadAppealsAsync(string url)
    {
        var response = await _ctx.AdminClient.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ReadJsonAsync<AppealResponse[]>()
               ?? throw new InvalidOperationException($"GET {url} returned no appeals.");
    }

    private sealed record AssignedActivity(Guid AssignmentId, Guid AssignedUserId);
}
