namespace Chronos.MainApi.Schedule.Contracts;

public record CreateActivityConstraintRequest(
    Guid ActivityId,
    string Key,
    string Value,
    int? WeekNum = null);
