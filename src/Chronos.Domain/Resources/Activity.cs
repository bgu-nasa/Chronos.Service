namespace Chronos.Domain.Resources;
public class Activity : ObjectInformation
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid AssignedUserId { get; set; }
    public required string ActivityType { get; set; }
    public int? ExpectedStudents { get; set; }
    public int Duration { get; set; }
}