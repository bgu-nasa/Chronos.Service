namespace Chronos.MainApi.Schedule.Contracts;

public record CreateAppealRequest(
    Guid AssignmentId,
    string Title,
    string Description);
