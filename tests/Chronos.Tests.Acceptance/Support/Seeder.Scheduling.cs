using Chronos.MainApi.Resources.Contracts;
using Chronos.MainApi.Schedule.Contracts;
using Chronos.Tests.Acceptance.Infrastructure;

namespace Chronos.Tests.Acceptance.Support;

/// <summary>
/// Seed helpers for building a schedulable setup: scheduling period, slots,
/// resources, subjects, activities, and assignments.
/// </summary>
public sealed partial class Seeder
{
    public async Task<SchedulingPeriodResponse> CreateSchedulingPeriodAsync(string name, DateTime fromDate, DateTime toDate)
    {
        var response = await client.PostJsonAsync("/api/schedule/scheduling/periods",
            new CreateSchedulingPeriodRequest(name, fromDate, toDate));
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync<SchedulingPeriodResponse>()
               ?? throw new InvalidOperationException("Create scheduling period returned no body.");
    }

    public async Task<SlotResponse> CreateSlotAsync(Guid schedulingPeriodId, WeekDays weekday, TimeSpan fromTime, TimeSpan toTime)
    {
        var response = await client.PostJsonAsync("/api/schedule/scheduling/slots",
            new CreateSlotRequest(schedulingPeriodId, weekday, fromTime, toTime));
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync<SlotResponse>()
               ?? throw new InvalidOperationException("Create slot returned no body.");
    }

    public async Task<ResourceTypeResponse> CreateResourceTypeAsync(Guid organizationId, string type)
    {
        var response = await client.PostJsonAsync("/api/resources/resource/types",
            new CreateResourceTypeRequest(organizationId, type));
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync<ResourceTypeResponse>()
               ?? throw new InvalidOperationException("Create resource type returned no body.");
    }

    public async Task<ResourceResponse> CreateResourceAsync(
        Guid organizationId, Guid resourceTypeId, string location, string identifier, int? capacity)
    {
        var response = await client.PostJsonAsync("/api/resources/resource",
            new CreateResourceRequest(organizationId, resourceTypeId, location, identifier, capacity));
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync<ResourceResponse>()
               ?? throw new InvalidOperationException("Create resource returned no body.");
    }

    public async Task<SubjectResponse> CreateSubjectAsync(
        Guid organizationId, Guid departmentId, Guid schedulingPeriodId, string code, string name)
    {
        var response = await client.PostJsonAsync(
            $"/api/department/{departmentId}/resources/subjects/Subject",
            new CreateSubjectRequest(organizationId, departmentId, schedulingPeriodId, code, name));
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync<SubjectResponse>()
               ?? throw new InvalidOperationException("Create subject returned no body.");
    }

    public async Task<ActivityResponse> CreateActivityAsync(
        Guid organizationId, Guid departmentId, Guid subjectId, Guid assignedUserId,
        string activityType, int? expectedStudents, int duration)
    {
        var response = await client.PostJsonAsync(
            $"/api/department/{departmentId}/resources/subjects/Subject/{subjectId}/activities",
            new CreateActivityRequest(organizationId, subjectId, assignedUserId, activityType, expectedStudents, duration));
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync<ActivityResponse>()
               ?? throw new InvalidOperationException("Create activity returned no body.");
    }

    public async Task<ActivityResponse> CreateActivityWithPrerequisitesAsync(Guid organizationId)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var dept = await CreateDepartmentAsync($"Activity Dept {suffix}");
        var from = DateTime.UtcNow.Date.AddDays(30);
        var period = await CreateSchedulingPeriodAsync(
            $"Activity Period {suffix}",
            from,
            from.AddMonths(4));
        var instructor = await CreateUserAsync($"activity-instructor-{suffix}@chronos.test");
        var subject = await CreateSubjectAsync(
            organizationId,
            dept.Id,
            Guid.Parse(period.Id),
            $"ACT{suffix}",
            $"Activity Subject {suffix}");

        return await CreateActivityAsync(
            organizationId,
            dept.Id,
            subject.Id,
            Guid.Parse(instructor.UserId),
            "Lecture",
            expectedStudents: 25,
            duration: 2);
    }

    public async Task<AssignmentResponse> CreateAssignmentAsync(Guid slotId, Guid resourceId, Guid activityId, int weekNum)
    {
        var response = await client.PostJsonAsync("/api/schedule/scheduling/assignments",
            new CreateAssignmentRequest(slotId, resourceId, activityId, weekNum));
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync<AssignmentResponse>()
               ?? throw new InvalidOperationException("Create assignment returned no body.");
    }
}
