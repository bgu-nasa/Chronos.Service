namespace Chronos.MainApi.Schedule.Contracts;

public record UserConstraintResponse(
    string Id,
    string UserId,
    string OrganizationId,
    string SchedulingPeriodId,
    int? WeekNum,
    string Key,
    string Value);
