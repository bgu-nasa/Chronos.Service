namespace Chronos.MainApi.Schedule.Contracts;

public record ActivityConstraintResponse(
    string Id,
    string ActivityId,
    string OrganizationId,
    int? WeekNum,
    string Key,
    string Value);
