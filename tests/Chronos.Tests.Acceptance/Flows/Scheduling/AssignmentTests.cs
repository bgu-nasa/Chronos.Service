using System.Net;
using Chronos.MainApi.Schedule.Contracts;
using Chronos.Tests.Acceptance.Infrastructure;
using Chronos.Tests.Acceptance.Support;
using FluentAssertions;

namespace Chronos.Tests.Acceptance.Flows.Scheduling;

/// <summary>
/// Acceptance coverage for assignment management: create, read (by id, filtered list,
/// by slot / by activity / by slot+resource), update, and delete. Each test uses a
/// distinct slot + week number so assignments never collide on the shared resource.
/// </summary>
[TestFixture]
[Category("Acceptance")]
public class AssignmentTests
{
    private AcceptanceContext _ctx = null!;
    private Guid _periodId;
    private Guid _resourceId;
    private Guid _activityId;
    private Guid _assignedUserId;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _ctx = await AcceptanceContext.CreateAsync("Assignment Acceptance Org");

        var dept = await _ctx.Seed.CreateDepartmentAsync("CS");
        var period = await _ctx.Seed.CreateSchedulingPeriodAsync(
            "Fall 2026", new DateTime(2026, 9, 1), new DateTime(2027, 1, 31));
        _periodId = Guid.Parse(period.Id);

        var type = await _ctx.Seed.CreateResourceTypeAsync(_ctx.OrganizationId, "Lecture Hall");
        var resource = await _ctx.Seed.CreateResourceAsync(_ctx.OrganizationId, type.Id, "Building A", "Hall-101", 200);
        _resourceId = resource.Id;

        var user = await _ctx.Seed.CreateUserAsync("assign-lecturer@chronos.test");
        _assignedUserId = Guid.Parse(user.UserId);

        var subject = await _ctx.Seed.CreateSubjectAsync(_ctx.OrganizationId, dept.Id, _periodId, "CS101", "Intro to CS");
        var activity = await _ctx.Seed.CreateActivityAsync(
            _ctx.OrganizationId, dept.Id, subject.Id, _assignedUserId, "Lecture", 30, 2);
        _activityId = activity.Id;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown() => _ctx.Dispose();

    private async Task<Guid> SeedSlotAsync(int fromHour)
    {
        var slot = await _ctx.Seed.CreateSlotAsync(
            _periodId, WeekDays.Monday, TimeSpan.FromHours(fromHour), TimeSpan.FromHours(fromHour + 2));
        return Guid.Parse(slot.Id);
    }

    [Test]
    public async Task GivenSetup_WhenCreateAssignment_ThenItIsRetrievableById()
    {
        var slotId = await SeedSlotAsync(8);

        var created = await _ctx.Seed.CreateAssignmentAsync(slotId, _resourceId, _activityId, weekNum: 1);

        var got = await (await _ctx.AdminClient.GetAsync($"/api/schedule/scheduling/assignments/{created.Id}"))
            .ReadJsonAsync<AssignmentResponse>();
        got.Should().NotBeNull();
        Guid.Parse(got!.SlotId).Should().Be(slotId);
        Guid.Parse(got.ResourceId).Should().Be(_resourceId);
        Guid.Parse(got.ActivityId).Should().Be(_activityId);
        got.WeekNum.Should().Be(1);
    }

    [Test]
    public async Task GivenAssignment_WhenListWithFilters_ThenEachFilterReturnsIt()
    {
        var slotId = await SeedSlotAsync(10);
        var created = await _ctx.Seed.CreateAssignmentAsync(slotId, _resourceId, _activityId, weekNum: 2);

        async Task AssertFilterContains(string query)
        {
            var list = await (await _ctx.AdminClient.GetAsync($"/api/schedule/scheduling/assignments{query}"))
                .ReadJsonAsync<AssignmentResponse[]>();
            list.Should().NotBeNull();
            list!.Select(a => a.Id).Should().Contain(created.Id,
                "filter '{0}' should include the created assignment", query);
        }

        await AssertFilterContains($"?slotId={slotId}");
        await AssertFilterContains($"?resourceId={_resourceId}");
        await AssertFilterContains($"?activityId={_activityId}");
        await AssertFilterContains($"?assignedUserId={_assignedUserId}");
        await AssertFilterContains($"?schedulingPeriodId={_periodId}");
    }

    [Test]
    public async Task GivenAssignment_WhenQueryBySlotActivityAndResource_ThenReturnsIt()
    {
        var slotId = await SeedSlotAsync(12);
        var created = await _ctx.Seed.CreateAssignmentAsync(slotId, _resourceId, _activityId, weekNum: 3);

        var bySlot = await (await _ctx.AdminClient.GetAsync($"/api/schedule/scheduling/slots/{slotId}/assignments"))
            .ReadJsonAsync<AssignmentResponse[]>();
        bySlot!.Select(a => a.Id).Should().Contain(created.Id);

        var byActivity = await (await _ctx.AdminClient.GetAsync(
                $"/api/schedule/scheduling/activities/{_activityId}/assignments"))
            .ReadJsonAsync<AssignmentResponse[]>();
        byActivity!.Select(a => a.Id).Should().Contain(created.Id);

        var bySlotAndResource = await (await _ctx.AdminClient.GetAsync(
                $"/api/schedule/scheduling/slots/{slotId}/resources/{_resourceId}/assignment"))
            .ReadJsonAsync<AssignmentResponse>();
        bySlotAndResource.Should().NotBeNull();
        bySlotAndResource!.Id.Should().Be(created.Id);
    }

    [Test]
    public async Task GivenAssignment_WhenUpdatedToAnotherSlot_ThenChangePersists()
    {
        var originalSlot = await SeedSlotAsync(14);
        var created = await _ctx.Seed.CreateAssignmentAsync(originalSlot, _resourceId, _activityId, weekNum: 4);

        var newSlot = await SeedSlotAsync(16);
        var update = await _ctx.AdminClient.PatchJsonAsync(
            $"/api/schedule/scheduling/assignments/{created.Id}",
            new UpdateAssignmentRequest(newSlot, _resourceId, _activityId, WeekNum: 4));
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var got = await (await _ctx.AdminClient.GetAsync($"/api/schedule/scheduling/assignments/{created.Id}"))
            .ReadJsonAsync<AssignmentResponse>();
        Guid.Parse(got!.SlotId).Should().Be(newSlot);
    }

    [Test]
    public async Task GivenAssignment_WhenDeleted_ThenItIsGone()
    {
        var slotId = await SeedSlotAsync(18);
        var created = await _ctx.Seed.CreateAssignmentAsync(slotId, _resourceId, _activityId, weekNum: 5);

        var delete = await _ctx.AdminClient.DeleteAsync($"/api/schedule/scheduling/assignments/{created.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getAfter = await _ctx.AdminClient.GetAsync($"/api/schedule/scheduling/assignments/{created.Id}");
        getAfter.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
