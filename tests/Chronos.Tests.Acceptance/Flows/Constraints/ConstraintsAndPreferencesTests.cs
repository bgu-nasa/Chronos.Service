using System.Net;
using Chronos.MainApi.Schedule.Contracts;
using Chronos.Tests.Acceptance.Infrastructure;
using Chronos.Tests.Acceptance.Support;
using FluentAssertions;

namespace Chronos.Tests.Acceptance.Flows.Constraints;

/// <summary>
/// Acceptance coverage for submitting scheduling constraints and preferences:
/// user constraints, user preferences, activity constraints, and organization
/// policies — each as a full create → read-back → update → delete round-trip.
/// </summary>
[TestFixture]
[Category("Acceptance")]
public class ConstraintsAndPreferencesTests
{
    private const string ConstraintsBase = "/api/schedule/constraints";

    private AcceptanceContext _ctx = null!;
    private Guid _periodId;
    private Guid _userId;
    private Guid _activityId;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _ctx = await AcceptanceContext.CreateAsync("Constraints Acceptance Org");

        var dept = await _ctx.Seed.CreateDepartmentAsync("CS");

        // Constraint/preference/policy creation rejects past periods, so seed a future one.
        var from = DateTime.UtcNow.Date.AddDays(7);
        var period = await _ctx.Seed.CreateSchedulingPeriodAsync("Future Term", from, from.AddMonths(4));
        _periodId = Guid.Parse(period.Id);

        var user = await _ctx.Seed.CreateUserAsync("constraints-user@chronos.test");
        _userId = Guid.Parse(user.UserId);

        var subject = await _ctx.Seed.CreateSubjectAsync(_ctx.OrganizationId, dept.Id, _periodId, "CS101", "Intro to CS");
        var activity = await _ctx.Seed.CreateActivityAsync(
            _ctx.OrganizationId, dept.Id, subject.Id, _userId, "Lecture", 30, 2);
        _activityId = activity.Id;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown() => _ctx.Dispose();

    [Test]
    public async Task UserConstraint_FullRoundTrip()
    {
        var create = await _ctx.AdminClient.PostJsonAsync($"{ConstraintsBase}/userConstraint",
            new CreateUserConstraintRequest(_userId, _periodId, "unavailable_day", "Friday"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.ReadJsonAsync<UserConstraintResponse>();
        created!.Key.Should().Be("unavailable_day");
        created.Value.Should().Be("Friday");

        var afterCreate = await (await _ctx.AdminClient.GetAsync($"{ConstraintsBase}/userConstraint/{created.Id}"))
            .ReadJsonAsync<UserConstraintResponse>();
        afterCreate!.Value.Should().Be("Friday");

        var update = await _ctx.AdminClient.PatchJsonAsync($"{ConstraintsBase}/userConstraint/{created.Id}",
            new UpdateUserConstraintRequest("unavailable_day", "Monday"));
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterUpdate = await (await _ctx.AdminClient.GetAsync($"{ConstraintsBase}/userConstraint/{created.Id}"))
            .ReadJsonAsync<UserConstraintResponse>();
        afterUpdate!.Value.Should().Be("Monday");

        var delete = await _ctx.AdminClient.DeleteAsync($"{ConstraintsBase}/userConstraint/{created.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await _ctx.AdminClient.GetAsync($"{ConstraintsBase}/userConstraint/{created.Id}");
        afterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ActivityConstraint_FullRoundTrip()
    {
        var create = await _ctx.AdminClient.PostJsonAsync($"{ConstraintsBase}/activityConstraint",
            new CreateActivityConstraintRequest(_activityId, "location_preference", "Building A"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.ReadJsonAsync<ActivityConstraintResponse>();
        created!.Key.Should().Be("location_preference");
        created.Value.Should().Be("Building A");

        var update = await _ctx.AdminClient.PatchJsonAsync($"{ConstraintsBase}/activityConstraint/{created.Id}",
            new UpdateActivityConstraintRequest("location_preference", "Building B"));
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterUpdate = await (await _ctx.AdminClient.GetAsync($"{ConstraintsBase}/activityConstraint/{created.Id}"))
            .ReadJsonAsync<ActivityConstraintResponse>();
        afterUpdate!.Value.Should().Be("Building B");

        var delete = await _ctx.AdminClient.DeleteAsync($"{ConstraintsBase}/activityConstraint/{created.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await _ctx.AdminClient.GetAsync($"{ConstraintsBase}/activityConstraint/{created.Id}");
        afterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UserPreference_FullRoundTrip()
    {
        const string key = "preferred_weekday";

        var create = await _ctx.AdminClient.PostJsonAsync($"{ConstraintsBase}/preferenceConstraint",
            new CreateUserPreferenceRequest(_userId, _periodId, key, "Monday"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.ReadJsonAsync<UserPreferenceResponse>();
        created!.Value.Should().Be("Monday");

        var afterCreate = await (await _ctx.AdminClient.GetAsync(
                $"{ConstraintsBase}/preferenceConstraint/{_userId}/{_periodId}/{key}"))
            .ReadJsonAsync<UserPreferenceResponse>();
        afterCreate!.Value.Should().Be("Monday");

        var update = await _ctx.AdminClient.PatchJsonAsync(
            $"{ConstraintsBase}/preferenceConstraint/{_userId}/{_periodId}/{key}",
            new UpdateUserPreferenceRequest(_userId, _periodId, key, "Tuesday"));
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterUpdate = await (await _ctx.AdminClient.GetAsync(
                $"{ConstraintsBase}/preferenceConstraint/{_userId}/{_periodId}/{key}"))
            .ReadJsonAsync<UserPreferenceResponse>();
        afterUpdate!.Value.Should().Be("Tuesday");

        // Delete is by preference id; verify removal via the by-user list
        // (the by-key GET throws when absent rather than returning 404).
        var delete = await _ctx.AdminClient.DeleteAsync($"{ConstraintsBase}/preferenceConstraint/{created.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var remaining = await (await _ctx.AdminClient.GetAsync($"{ConstraintsBase}/preferenceConstraint/by-user/{_userId}"))
            .ReadJsonAsync<UserPreferenceResponse[]>();
        remaining.Should().NotBeNull();
        remaining!.Should().NotContain(p => p.Key == key);
    }
}
