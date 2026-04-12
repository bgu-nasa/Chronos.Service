namespace Chronos.MainApi.Schedule.Contracts;

public record CreateAssignmentRequest(
    Guid SlotId,
    Guid ResourceId,
    Guid ActivityId,
    int? WeekNum = null);
