namespace Chronos.Domain.Schedule;

public class UserConstraint : ObjectInformation
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public Guid SchedulingPeriodId { get; set; }
    public int? WeekNum { get; set; }
    public required string Key { get; set; }
    public required string Value { get; set; }
}
