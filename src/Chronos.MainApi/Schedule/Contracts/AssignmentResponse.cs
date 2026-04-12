namespace Chronos.MainApi.Schedule.Contracts;

public record AssignmentResponse(
    string Id,
    string OrganizationId,
    string SlotId,
    string ResourceId,
    string ActivityId,
    int? WeekNum);
