namespace Chronos.MainApi.Schedule.Contracts;

public record UpdateAssignmentRequest(
    Guid SlotId,
    Guid ResourceId,
    Guid ActivityId,
    int? WeekNum = null);
