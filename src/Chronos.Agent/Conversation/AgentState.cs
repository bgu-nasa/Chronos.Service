namespace Chronos.Agent.Conversation;

/// <summary>
/// States of the agent's finite state machine.
/// Discovery → Drafting → Submit → (Revision ↔ Submit) → Approved
/// </summary>
public enum AgentState
{
    Discovery,
    Drafting,
    Submit,
    Revision,
    Approved
}
