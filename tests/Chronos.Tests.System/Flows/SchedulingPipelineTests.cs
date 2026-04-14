using System.Net;
using Chronos.MainApi.Auth.Contracts;
using Chronos.MainApi.Management.Contracts;
using Chronos.MainApi.Resources.Contracts;
using Chronos.MainApi.Schedule.Contracts;
using Chronos.Tests.System.Infrastructure;
using FluentAssertions;
using NSubstitute;

namespace Chronos.Tests.System.Flows;

/// <summary>
/// End-to-end tests that exercise the full scheduling pipeline:
/// register → create org structure → define resources → create schedule → trigger batch.
/// </summary>
[TestFixture]
[Category("E2E")]
public class SchedulingPipelineTests
{
    private const string ValidInviteCode = "hih";

    private ChronosApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _orgId;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new ChronosApiFactory();
        _client = _factory.CreateClient();

        var regResponse = await _client.PostJsonAsync("/api/auth/register",
            new RegisterRequest(
                AdminUser: new CreateUserRequest("sched-admin@chronos.dev", "Schedule", "Admin", "Passw0rd1"),
                OrganizationName: "Scheduling Pipeline Org",
                Plan: "free",
                InviteCode: ValidInviteCode));

        var auth = await regResponse.ReadJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new global::System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.Token);

        var orgInfo = await (await _client.GetAsync("/api/management/organization/info"))
            .ReadJsonAsync<OrganizationInformation>();
        _orgId = orgInfo!.Id;
        _client.DefaultRequestHeaders.Add("x-org-id", _orgId.ToString());
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test, Order(1)]
    public async Task GivenAuthenticatedAdmin_WhenCreateDepartment_ThenReturns201()
    {
        var response = await _client.PostJsonAsync("/api/management/department",
            new DepartmentRequest("Engineering Faculty"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dept = await response.ReadJsonAsync<DepartmentResponse>();
        dept!.Name.Should().Be("Engineering Faculty");
    }

    [Test, Order(2)]
    public async Task GivenExistingOrg_WhenCreateSchedulingPeriod_ThenReturns201()
    {
        var response = await _client.PostJsonAsync("/api/schedule/scheduling/periods",
            new CreateSchedulingPeriodRequest(
                "Fall 2026",
                new DateTime(2026, 9, 1),
                new DateTime(2027, 1, 31)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var period = await response.ReadJsonAsync<SchedulingPeriodResponse>();
        period!.Name.Should().Be("Fall 2026");
    }

    [Test, Order(3)]
    public async Task GivenSchedulingPeriod_WhenCreateSlots_ThenReturns201()
    {
        var periodsResponse = await _client.GetAsync("/api/schedule/scheduling/periods");
        var periods = await periodsResponse.ReadJsonAsync<SchedulingPeriodResponse[]>();
        var periodId = Guid.Parse(periods!.First().Id);

        var mondaySlot = await _client.PostJsonAsync("/api/schedule/scheduling/slots",
            new CreateSlotRequest(periodId, WeekDays.Monday,
                TimeSpan.FromHours(9), TimeSpan.FromHours(11)));

        var wednesdaySlot = await _client.PostJsonAsync("/api/schedule/scheduling/slots",
            new CreateSlotRequest(periodId, WeekDays.Wednesday,
                TimeSpan.FromHours(14), TimeSpan.FromHours(16)));

        mondaySlot.StatusCode.Should().Be(HttpStatusCode.Created);
        wednesdaySlot.StatusCode.Should().Be(HttpStatusCode.Created);

        var mondaySlotBody = await mondaySlot.ReadJsonAsync<SlotResponse>();
        mondaySlotBody!.Weekday.Should().Be(WeekDays.Monday);
    }

    [Test, Order(4)]
    public async Task GivenExistingOrg_WhenCreateResourceTypeAndResource_ThenReturns201()
    {
        var typeResponse = await _client.PostJsonAsync("/api/resources/resource/types",
            new CreateResourceTypeRequest(Guid.NewGuid(), _orgId, "Lecture Hall"));
        typeResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var resourceType = await typeResponse.ReadJsonAsync<ResourceTypeResponse>();

        var resourceResponse = await _client.PostJsonAsync("/api/resources/resource",
            new CreateResourceRequest(
                Guid.NewGuid(), _orgId, resourceType!.Id, "Building A", "Hall-101", 200));

        resourceResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var resource = await resourceResponse.ReadJsonAsync<ResourceResponse>();
        resource!.Identifier.Should().Be("Hall-101");
        resource.Capacity.Should().Be(200);
    }

    [Test, Order(5)]
    public async Task GivenDepartmentAndPeriod_WhenCreateSubjectAndActivity_ThenReturns201()
    {
        var depts = await (await _client.GetAsync("/api/management/department"))
            .ReadJsonAsync<DepartmentResponse[]>();
        var deptId = depts!.First().Id;

        var periods = await (await _client.GetAsync("/api/schedule/scheduling/periods"))
            .ReadJsonAsync<SchedulingPeriodResponse[]>();
        var periodId = Guid.Parse(periods!.First().Id);

        var subjectResponse = await _client.PostJsonAsync(
            $"/api/department/{deptId}/resources/subjects/Subject",
            new CreateSubjectRequest(
                Guid.NewGuid(), _orgId, deptId, periodId, "CS201", "Data Structures"));
        subjectResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var subject = await subjectResponse.ReadJsonAsync<SubjectResponse>();

        // Create user to be assigned to the activity
        var userResponse = await _client.PostJsonAsync("/api/user",
            new CreateUserRequest("lecturer@chronos.dev", "Prof", "Smith", "Passw0rd2"));
        var user = await userResponse.ReadJsonAsync<CreateUserResponse>();

        var activityResponse = await _client.PostJsonAsync(
            $"/api/department/{deptId}/resources/subjects/Subject/{subject!.Id}/activities",
            new CreateActivityRequest(
                Guid.NewGuid(), _orgId, subject.Id,
                Guid.Parse(user!.UserId), "Lecture", 120, 2));

        activityResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var activity = await activityResponse.ReadJsonAsync<ActivityResponse>();
        activity!.ActivityType.Should().Be("Lecture");
        activity.Duration.Should().Be(2);
    }

    [Test, Order(6)]
    public async Task GivenFullDataSetup_WhenTriggerBatchScheduling_ThenReturns202AndPublishesMessage()
    {
        var periods = await (await _client.GetAsync("/api/schedule/scheduling/periods"))
            .ReadJsonAsync<SchedulingPeriodResponse[]>();
        var periodId = Guid.Parse(periods!.First().Id);

        var response = await _client.PostAsync(
            $"/api/schedule/scheduling/periods/{periodId}/batch-schedule", null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("submitted successfully");

        await _factory.MockMessagePublisher.Received(1)
            .PublishAsync(
                Arg.Is<Chronos.Domain.Schedule.Messages.SchedulePeriodRequest>(
                    r => r.SchedulingPeriodId == periodId && r.OrganizationId == _orgId),
                Arg.Is("request.batch"));
    }

    [Test, Order(7)]
    public async Task GivenCreatedData_WhenQueryAllSlots_ThenReturnsSlotsList()
    {
        var response = await _client.GetAsync("/api/schedule/scheduling/slots");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var slots = await response.ReadJsonAsync<SlotResponse[]>();
        slots.Should().NotBeNull();
        slots!.Length.Should().BeGreaterThanOrEqualTo(2);
    }

    [Test, Order(8)]
    public async Task GivenCreatedData_WhenQueryActivitiesBySubject_ThenReturnsActivities()
    {
        var depts = await (await _client.GetAsync("/api/management/department"))
            .ReadJsonAsync<DepartmentResponse[]>();
        var deptId = depts!.First().Id;

        var subjects = await (await _client.GetAsync(
                $"/api/department/{deptId}/resources/subjects/Subject"))
            .ReadJsonAsync<SubjectResponse[]>();
        var subjectId = subjects!.First().Id;

        var response = await _client.GetAsync(
            $"/api/department/{deptId}/resources/subjects/Subject/{subjectId}/activities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var activities = await response.ReadJsonAsync<ActivityResponse[]>();
        activities.Should().NotBeNull();
        activities!.Length.Should().BeGreaterThanOrEqualTo(1);
        activities.First().ActivityType.Should().Be("Lecture");
    }
}
