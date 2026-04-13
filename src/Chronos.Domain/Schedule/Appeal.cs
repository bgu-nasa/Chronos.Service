namespace Chronos.Domain.Schedule;

public class Appeal : ObjectInformation
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid AssignmentId { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
}
