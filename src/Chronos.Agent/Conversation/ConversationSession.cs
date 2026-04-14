using Chronos.Agent.Domain;

namespace Chronos.Agent.Conversation;

public sealed class ConversationSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid SchedulingPeriodId { get; set; }
    public AgentState State { get; set; } = AgentState.Discovery;
    public List<ChatMessage> Messages { get; set; } = [];
    public ConstraintDraft? Draft { get; set; }
}
