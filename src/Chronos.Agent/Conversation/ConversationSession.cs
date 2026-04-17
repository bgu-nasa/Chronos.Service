using Chronos.Agent.Domain;

namespace Chronos.Agent.Conversation;

/// <summary>
/// Represents an active conversation session between a user and the scheduling agent.
/// Enforces valid FSM state transitions and provides guarded mutation methods.
/// </summary>
public class ConversationSession
{
    private static readonly Dictionary<AgentState, HashSet<AgentState>> AllowedTransitions = new()
    {
        [AgentState.Discovery] = new() { AgentState.Drafting },
        [AgentState.Drafting] = new() { AgentState.Submit },
        [AgentState.Submit] = new() { AgentState.Revision, AgentState.Approved },
        [AgentState.Revision] = new() { AgentState.Submit },
        [AgentState.Approved] = new() // terminal state
    };

    private readonly List<ChatMessage> _messages = new();

    public Guid Id { get; }
    public Guid UserId { get; }
    public Guid OrganizationId { get; }
    public Guid SchedulingPeriodId { get; }
    public AgentState State { get; private set; }
    public IReadOnlyList<ChatMessage> Messages => _messages.AsReadOnly();
    public ConstraintDraft? Draft { get; private set; }

    public ConversationSession(Guid userId, Guid organizationId, Guid schedulingPeriodId)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        OrganizationId = organizationId;
        SchedulingPeriodId = schedulingPeriodId;
        State = AgentState.Discovery;
    }

    public void AddUserMessage(string content)
        => _messages.Add(new ChatMessage("user", content));

    public void AddAssistantMessage(string content)
        => _messages.Add(new ChatMessage("assistant", content));

    public void AddSystemMessage(string content)
        => _messages.Add(new ChatMessage("system", content));

    public void TransitionTo(AgentState newState)
    {
        if (!AllowedTransitions.TryGetValue(State, out var allowed) || !allowed.Contains(newState))
            throw new InvalidOperationException(
                $"Cannot transition from {State} to {newState}.");

        State = newState;
    }

    public void SetDraft(ConstraintDraft draft)
    {
        Draft = draft ?? throw new ArgumentNullException(nameof(draft));
    }
}
