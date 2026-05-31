namespace Chronos.MainApi.Schedule.Contracts;

public record UpdateUserConstraintRequest(
    Guid UserId,
    Guid SchedulingPeriodId,
    string Key,
    string Value,
    int? WeekNum = null);
