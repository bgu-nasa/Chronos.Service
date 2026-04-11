namespace Chronos.MainApi.Schedule.Contracts;

public record UpdateActivityConstraintRequest(
    string Key,
    string Value,
    int? WeekNum = null);
