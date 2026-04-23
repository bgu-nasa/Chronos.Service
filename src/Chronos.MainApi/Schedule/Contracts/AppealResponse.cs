namespace Chronos.MainApi.Schedule.Contracts;

public record AppealResponse(
    string Id,
    string OrganizationId,
    string AssignmentId,
    string Title,
    string Description,
    DateTime CreatedAt,
    DateTime UpdatedAt);
