using Chronos.Agent.Conversation;
using Chronos.Agent.Domain;
using Chronos.Agent.Extraction;

namespace Chronos.MainApi.Agent.Contracts;

public record AgentSessionResponse(
    Guid SessionId,
    AgentState State,
    string? AssistantMessage,
    DraftResponse? Draft,
    IReadOnlySet<AgentAction> AllowedActions,
    IReadOnlyList<ValidationIssueResponse> ValidationIssues);

public record DraftResponse(
    IReadOnlyList<DraftItemResponse> HardConstraints,
    IReadOnlyList<DraftItemResponse> SoftPreferences);

public record DraftItemResponse(string Key, string Value);

public record ValidationIssueResponse(string Kind, string Key, string Value, string Reason);
