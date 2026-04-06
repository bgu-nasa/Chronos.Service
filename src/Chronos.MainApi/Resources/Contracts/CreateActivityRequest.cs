namespace Chronos.MainApi.Resources.Contracts;

public sealed record CreateActivityRequest(
    Guid Id,
    Guid OrganizationId,
    Guid SubjectId,
    Guid AssignedUserId,
    string ActivityType,
    int? ExpectedStudents,
    int Duration);