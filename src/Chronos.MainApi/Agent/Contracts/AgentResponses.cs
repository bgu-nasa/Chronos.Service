using Chronos.Agent.Conversation;
using Chronos.Agent.Domain;

namespace Chronos.MainApi.Agent.Contracts;

public record AgentSessionResponse(
    Guid SessionId,
    AgentState State,
    string? AssistantMessage,
    DraftResponse? Draft,
    IReadOnlySet<AgentAction> AllowedActions);

public record DraftResponse(
    IReadOnlyList<DraftItemResponse> HardConstraints,
    IReadOnlyList<DraftItemResponse> SoftPreferences);

public record DraftItemResponse(string Key, string Value);
