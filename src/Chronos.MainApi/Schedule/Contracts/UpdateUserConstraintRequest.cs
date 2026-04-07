namespace Chronos.MainApi.Schedule.Contracts;

public record UpdateUserConstraintRequest(
    string Key,
    string Value,
    int? WeekNum = null);
