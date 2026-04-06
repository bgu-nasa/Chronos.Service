namespace Chronos.Domain.Schedule.Messages;

public record HandleConstraintChangeRequest(
    Guid ActivityConstraintId,
    Guid OrganizationId,
    Guid SchedulingPeriodId,
    ConstraintScope Scope = ConstraintScope.Activity,
    ConstraintChangeOperation Operation = ConstraintChangeOperation.Created,
    Guid? ActivityId = null,
    Guid? UserId = null,
    SchedulingMode Mode = SchedulingMode.Online
);

