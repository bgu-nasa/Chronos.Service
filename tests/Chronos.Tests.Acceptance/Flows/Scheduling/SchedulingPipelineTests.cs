using System.Net;
using Chronos.Domain.Schedule.Messages;
using Chronos.MainApi.Resources.Contracts;
using Chronos.MainApi.Schedule.Contracts;
using Chronos.Tests.Acceptance.Infrastructure;
using Chronos.Tests.Acceptance.Support;
using FluentAssertions;
using NSubstitute;

namespace Chronos.Tests.Acceptance.Flows.Scheduling;

/// <summary>
/// Exercises the full scheduling pipeline: build the schedulable setup
/// (department → period → slots → resource → subject → activity) and trigger a
/// batch run. Self-seeding via <see cref="AcceptanceContext"/>; no shared ordered state.
/// </summary>
[TestFixture]
[Category("Acceptance")]
public class SchedulingPipelineTests
{
    private AcceptanceContext _ctx = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp() => _ctx = await AcceptanceContext.CreateAsync("Scheduling Pipeline Org");

    [OneTimeTearDown]
    public void OneTimeTearDown() => _ctx.Dispose();

    [Test]
    public async Task GivenFullSetup_WhenTriggerBatchSchedule_ThenAcceptedAndPublishesRequest()
    {
        var dept = await _ctx.Seed.CreateDepartmentAsync("Engineering Faculty");
        var period = await _ctx.Seed.CreateSchedulingPeriodAsync(
            "Fall 2026", new DateTime(2026, 9, 1), new DateTime(2027, 1, 31));
        var periodId = Guid.Parse(period.Id);

        await _ctx.Seed.CreateSlotAsync(periodId, WeekDays.Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(11));
        await _ctx.Seed.CreateSlotAsync(periodId, WeekDays.Wednesday, TimeSpan.FromHours(14), TimeSpan.FromHours(16));

        var type = await _ctx.Seed.CreateResourceTypeAsync(_ctx.OrganizationId, "Lecture Hall");
        await _ctx.Seed.CreateResourceAsync(_ctx.OrganizationId, type.Id, "Building A", "Hall-101", 200);

        var user = await _ctx.Seed.CreateUserAsync("lecturer@chronos.test");
        var subject = await _ctx.Seed.CreateSubjectAsync(
            _ctx.OrganizationId, dept.Id, periodId, "CS201", "Data Structures");
        await _ctx.Seed.CreateActivityAsync(
            _ctx.OrganizationId, dept.Id, subject.Id, Guid.Parse(user.UserId), "Lecture", 30, 2);

        var response = await _ctx.AdminClient.PostAsync(
            $"/api/schedule/scheduling/periods/{periodId}/batch-schedule", null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        await _ctx.Factory.MockMessagePublisher.Received(1).PublishAsync(
            Arg.Is<SchedulePeriodRequest>(r =>
                r.SchedulingPeriodId == periodId && r.OrganizationId == _ctx.OrganizationId),
            "request.batch");
    }

    [Test]
    public async Task GivenSchedulingSetup_WhenQuerySlotsAndActivities_ThenReturnsSeededData()
    {
        var dept = await _ctx.Seed.CreateDepartmentAsync("Science Faculty");
        var period = await _ctx.Seed.CreateSchedulingPeriodAsync(
            "Spring 2027", new DateTime(2027, 2, 1), new DateTime(2027, 6, 30));
        var periodId = Guid.Parse(period.Id);

        var slot = await _ctx.Seed.CreateSlotAsync(periodId, WeekDays.Wednesday, TimeSpan.FromHours(10), TimeSpan.FromHours(12));
        var user = await _ctx.Seed.CreateUserAsync("prof-readback@chronos.test");
        var subject = await _ctx.Seed.CreateSubjectAsync(_ctx.OrganizationId, dept.Id, periodId, "PHY101", "Physics");
        var activity = await _ctx.Seed.CreateActivityAsync(
            _ctx.OrganizationId, dept.Id, subject.Id, Guid.Parse(user.UserId), "Lab", 25, 3);

        var slots = await (await _ctx.AdminClient.GetAsync($"/api/schedule/scheduling/periods/{periodId}/slots"))
            .ReadJsonAsync<SlotResponse[]>();
        slots.Should().NotBeNull();
        slots!.Select(s => s.Id).Should().Contain(slot.Id);

        var activities = await (await _ctx.AdminClient.GetAsync(
                $"/api/department/{dept.Id}/resources/subjects/Subject/{subject.Id}/activities"))
            .ReadJsonAsync<ActivityResponse[]>();
        activities.Should().NotBeNull();
        activities!.Should().ContainSingle(a => a.Id == activity.Id)
            .Which.ActivityType.Should().Be("Lab");
    }
}
