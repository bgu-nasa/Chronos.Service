namespace Chronos.Domain.Schedule;

public class AssignmentQuery
{
    public Guid? OrganizationId { get; set; }
    public Guid? SlotId { get; set; }
    public Guid? ResourceId { get; set; }
    public Guid? ActivityId { get; set; }
    public Guid? SchedulingPeriodId { get; set; }
    public Guid? UserId { get; set; }
}
