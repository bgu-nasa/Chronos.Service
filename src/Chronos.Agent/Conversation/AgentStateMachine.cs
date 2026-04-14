namespace Chronos.Agent.Conversation;

public sealed class AgentStateMachine
{
    public void MoveToDrafting(ConversationSession session)
    {
        EnsureState(session, AgentState.Discovery, AgentState.Revision, AgentState.Submit);
        session.State = AgentState.Drafting;
    }

    public void MoveToSubmit(ConversationSession session)
    {
        EnsureState(session, AgentState.Drafting, AgentState.Revision);
        session.State = AgentState.Submit;
    }

    public void MoveToRevision(ConversationSession session)
    {
        EnsureState(session, AgentState.Submit, AgentState.Drafting);
        session.State = AgentState.Revision;
    }

    public void MoveToApproved(ConversationSession session)
    {
        EnsureState(session, AgentState.Submit);
        session.State = AgentState.Approved;
    }

    private static void EnsureState(ConversationSession session, params AgentState[] allowed)
    {
        if (!allowed.Contains(session.State))
        {
            throw new InvalidOperationException($"Cannot transition from {session.State}.");
        }
    }
}
