using Chronos.Agent.Domain;

namespace Chronos.Agent.Conversation;

/// <summary>
/// User-facing actions the agent supports at any given state.
/// </summary>
public enum AgentAction
{
    ContinueConversation,
    Submit,
    Approve,
    Revise
}

/// <summary>
/// Finite state machine that drives agent conversation flow.
/// Wraps a ConversationSession and enforces valid action sequencing.
/// </summary>
public class AgentStateMachine
{
    private static readonly Dictionary<AgentState, HashSet<AgentState>> Transitions = new()
    {
        [AgentState.Discovery] = new() { AgentState.Drafting },
        [AgentState.Drafting]  = new() { AgentState.Submit },
        [AgentState.Submit]    = new() { AgentState.Revision, AgentState.Approved },
        [AgentState.Revision]  = new() { AgentState.Submit },
        [AgentState.Approved]  = new()
    };

    private readonly ConversationSession _session;

    public AgentStateMachine(ConversationSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public AgentState CurrentState => _session.State;

    public static bool CanTransition(AgentState from, AgentState to)
    {
        return Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    /// <summary>
    /// Records a user message. Valid in Discovery and Revision states.
    /// </summary>
    public AgentAction ProcessUserMessage(string content)
    {
        if (_session.State != AgentState.Discovery && _session.State != AgentState.Revision)
            throw new InvalidOperationException(
                $"Cannot process user messages in {_session.State} state.");

        _session.AddUserMessage(content);
        return AgentAction.ContinueConversation;
    }

    /// <summary>
    /// Moves to Submit state with the given draft. 
    /// Transitions Discovery→Drafting→Submit or Revision→Submit.
    /// </summary>
    public void RequestSubmit(ConstraintDraft draft)
    {
        if (draft.HardConstraints.Count == 0 && draft.SoftPreferences.Count == 0)
            throw new InvalidOperationException("Cannot submit an empty draft.");

        if (_session.State == AgentState.Discovery)
        {
            _session.TransitionTo(AgentState.Drafting);
            _session.SetDraft(draft);
            _session.TransitionTo(AgentState.Submit);
        }
        else if (_session.State == AgentState.Revision)
        {
            _session.SetDraft(draft);
            _session.TransitionTo(AgentState.Submit);
        }
        else
        {
            throw new InvalidOperationException(
                $"Cannot request submit from {_session.State} state.");
        }
    }

    /// <summary>
    /// Approves the current draft. Only valid from Submit state.
    /// </summary>
    public void Approve()
    {
        if (_session.State != AgentState.Submit)
            throw new InvalidOperationException(
                $"Cannot approve from {_session.State} state. Must be in Submit state.");

        _session.TransitionTo(AgentState.Approved);
    }

    /// <summary>
    /// Returns to Revision state from Submit for further edits.
    /// </summary>
    public void RequestRevision()
    {
        if (_session.State != AgentState.Submit)
            throw new InvalidOperationException(
                $"Cannot revise from {_session.State} state. Must be in Submit state.");

        _session.TransitionTo(AgentState.Revision);
    }

    /// <summary>
    /// Returns the set of actions available in the current state.
    /// </summary>
    public IReadOnlySet<AgentAction> GetAllowedActions()
    {
        return _session.State switch
        {
            AgentState.Discovery => new HashSet<AgentAction> { AgentAction.ContinueConversation, AgentAction.Submit },
            AgentState.Drafting  => new HashSet<AgentAction> { AgentAction.Submit },
            AgentState.Submit    => new HashSet<AgentAction> { AgentAction.Approve, AgentAction.Revise },
            AgentState.Revision  => new HashSet<AgentAction> { AgentAction.ContinueConversation, AgentAction.Submit },
            // AgentState.Approved  => new HashSet<AgentAction>(),
            _ => new HashSet<AgentAction>()
        };
    }
}
