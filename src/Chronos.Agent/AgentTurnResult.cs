using Chronos.Agent.Conversation;
using Chronos.Agent.Domain;

namespace Chronos.Agent;

public enum AgentMode
{
    Conversation,
    Submit,
    Approved
}

public sealed class AgentTurnResult
{
    public Guid SessionId { get; init; }
    public AgentState State { get; init; }
    public AgentMode Mode { get; init; }
    public string Message { get; init; } = string.Empty;
    public ConstraintDraft? Draft { get; init; }
    public List<string> Errors { get; init; } = [];
}
