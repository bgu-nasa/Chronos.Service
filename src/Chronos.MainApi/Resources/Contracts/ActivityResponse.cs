namespace Chronos.MainApi.Resources.Contracts;

public sealed record ActivityResponse(
    Guid Id,
    Guid OrganizationId,
    Guid SubjectId,
    Guid AssignedUserId,
    string ActivityType,
    int? ExpectedStudents,
    int Duration);