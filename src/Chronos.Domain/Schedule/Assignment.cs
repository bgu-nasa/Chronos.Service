namespace Chronos.Domain.Schedule;

public class Assignment : ObjectInformation
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid SlotId { get; set; }
    public Guid ResourceId { get; set; }
    public Guid ActivityId { get; set; }
    public int? WeekNum { get; set; }
}
