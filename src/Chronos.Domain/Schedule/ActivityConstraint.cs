namespace Chronos.Domain.Schedule;

public class ActivityConstraint : ObjectInformation
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ActivityId { get; set; }
    public int? WeekNum { get; set; }
    public required string Key { get; set; }
    public required string Value { get; set; }
}
