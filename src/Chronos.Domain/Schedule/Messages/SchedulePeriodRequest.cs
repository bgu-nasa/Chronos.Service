namespace Chronos.Domain.Schedule.Messages;

public record SchedulePeriodRequest(
    Guid SchedulingPeriodId,
    Guid OrganizationId,
    SchedulingMode Mode = SchedulingMode.Batch,
    Guid? InitiatedByUserId = null
);

