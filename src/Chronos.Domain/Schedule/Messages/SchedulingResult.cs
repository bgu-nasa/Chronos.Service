namespace Chronos.Domain.Schedule.Messages;

public record SchedulingResult(
    Guid RequestId,
    bool Success,
    int AssignmentsCreated,
    int AssignmentsModified,
    List<Guid> UnscheduledActivityIds,
    string? FailureReason,
    Guid? InitiatedByUserId = null
);

