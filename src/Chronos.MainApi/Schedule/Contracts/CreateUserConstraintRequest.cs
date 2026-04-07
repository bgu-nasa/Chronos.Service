namespace Chronos.MainApi.Schedule.Contracts;

public record CreateUserConstraintRequest(
    Guid UserId,
    Guid SchedulingPeriodId,
    string Key,
    string Value,
    int? WeekNum = null);
