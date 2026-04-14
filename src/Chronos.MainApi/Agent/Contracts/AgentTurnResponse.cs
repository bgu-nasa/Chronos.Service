namespace Chronos.MainApi.Agent.Contracts;

public record AgentTurnResponse(
    string SessionId,
    string State,
    string Mode,
    string Message,
    List<AgentDraftItemResponse> HardConstraints,
    List<AgentDraftItemResponse> SoftPreferences,
    List<string> Errors);
