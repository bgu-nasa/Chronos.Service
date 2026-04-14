namespace Chronos.MainApi.Agent.Contracts;

public record ApproveAgentSessionResponse(
    string SessionId,
    int CreatedHardConstraints,
    int CreatedSoftPreferences);
