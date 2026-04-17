using Chronos.Agent.Conversation;
using Chronos.Agent.Domain;

namespace Chronos.Tests.Agent;

public class AgentStateMachineTests
{
    private static ConversationSession CreateSession() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    // --- CanTransition tests ---

    [Theory]
    [InlineData(AgentState.Discovery, AgentState.Drafting, true)]
    [InlineData(AgentState.Drafting, AgentState.Submit, true)]
    [InlineData(AgentState.Submit, AgentState.Revision, true)]
    [InlineData(AgentState.Submit, AgentState.Approved, true)]
    [InlineData(AgentState.Revision, AgentState.Submit, true)]
    [InlineData(AgentState.Discovery, AgentState.Approved, false)]
    [InlineData(AgentState.Discovery, AgentState.Submit, false)]
    [InlineData(AgentState.Approved, AgentState.Discovery, false)]
    [InlineData(AgentState.Approved, AgentState.Revision, false)]
    [InlineData(AgentState.Drafting, AgentState.Approved, false)]
    public void CanTransition_ReturnsExpected(AgentState from, AgentState to, bool expected)
    {
        Assert.Equal(expected, AgentStateMachine.CanTransition(from, to));
    }

    // --- ProcessUserMessage in Discovery ---

    [Fact]
    public void ProcessUserMessage_InDiscovery_StaysInDiscovery()
    {
        var session = CreateSession();
        var fsm = new AgentStateMachine(session);

        var action = fsm.ProcessUserMessage("I prefer mornings");

        Assert.Equal(AgentState.Discovery, session.State);
        Assert.Equal(AgentAction.ContinueConversation, action);
    }

    [Fact]
    public void ProcessUserMessage_InDiscovery_AddsUserMessage()
    {
        var session = CreateSession();
        var fsm = new AgentStateMachine(session);

        fsm.ProcessUserMessage("I prefer mornings");

        Assert.Single(session.Messages);
        Assert.Equal("user", session.Messages[0].Role);
        Assert.Equal("I prefer mornings", session.Messages[0].Content);
    }

    // --- RequestSubmit transitions ---

    [Fact]
    public void RequestSubmit_FromDiscovery_TransitionsThroughDraftingToSubmit()
    {
        var session = CreateSession();
        var fsm = new AgentStateMachine(session);
        var draft = new ConstraintDraft();
        draft.AddHardConstraint("avoid_weekday", "Friday");

        fsm.RequestSubmit(draft);

        Assert.Equal(AgentState.Submit, session.State);
        Assert.NotNull(session.Draft);
    }

    [Fact]
    public void RequestSubmit_WithEmptyDraft_Throws()
    {
        var session = CreateSession();
        var fsm = new AgentStateMachine(session);
        var draft = new ConstraintDraft(); // empty

        Assert.Throws<InvalidOperationException>(() => fsm.RequestSubmit(draft));
    }

    [Fact]
    public void RequestSubmit_FromApproved_Throws()
    {
        var session = CreateSession();
        session.TransitionTo(AgentState.Drafting);
        session.TransitionTo(AgentState.Submit);
        session.TransitionTo(AgentState.Approved);
        var fsm = new AgentStateMachine(session);
        var draft = new ConstraintDraft();
        draft.AddHardConstraint("avoid_weekday", "Friday");

        Assert.Throws<InvalidOperationException>(() => fsm.RequestSubmit(draft));
    }

    // --- Approve ---

    [Fact]
    public void Approve_FromSubmit_TransitionsToApproved()
    {
        var session = CreateSession();
        var fsm = new AgentStateMachine(session);
        var draft = new ConstraintDraft();
        draft.AddHardConstraint("avoid_weekday", "Friday");
        fsm.RequestSubmit(draft);

        fsm.Approve();

        Assert.Equal(AgentState.Approved, session.State);
    }

    [Fact]
    public void Approve_FromDiscovery_Throws()
    {
        var session = CreateSession();
        var fsm = new AgentStateMachine(session);

        Assert.Throws<InvalidOperationException>(() => fsm.Approve());
    }

    // --- Revise ---

    [Fact]
    public void RequestRevision_FromSubmit_TransitionsToRevision()
    {
        var session = CreateSession();
        var fsm = new AgentStateMachine(session);
        var draft = new ConstraintDraft();
        draft.AddHardConstraint("avoid_weekday", "Friday");
        fsm.RequestSubmit(draft);

        fsm.RequestRevision();

        Assert.Equal(AgentState.Revision, session.State);
    }

    [Fact]
    public void ProcessUserMessage_InRevision_StaysInRevision()
    {
        var session = CreateSession();
        var fsm = new AgentStateMachine(session);
        var draft = new ConstraintDraft();
        draft.AddHardConstraint("avoid_weekday", "Friday");
        fsm.RequestSubmit(draft);
        fsm.RequestRevision();

        var action = fsm.ProcessUserMessage("Also avoid Tuesday");

        Assert.Equal(AgentState.Revision, session.State);
        Assert.Equal(AgentAction.ContinueConversation, action);
    }

    [Fact]
    public void RequestSubmit_FromRevision_BackToSubmit()
    {
        var session = CreateSession();
        var fsm = new AgentStateMachine(session);
        var draft = new ConstraintDraft();
        draft.AddHardConstraint("avoid_weekday", "Friday");
        fsm.RequestSubmit(draft);
        fsm.RequestRevision();

        var newDraft = new ConstraintDraft();
        newDraft.AddHardConstraint("avoid_weekday", "Friday");
        newDraft.AddHardConstraint("avoid_weekday", "Tuesday");
        fsm.RequestSubmit(newDraft);

        Assert.Equal(AgentState.Submit, session.State);
        Assert.Equal(2, session.Draft!.HardConstraints.Count);
    }

    // --- GetAllowedActions ---

    [Fact]
    public void GetAllowedActions_InDiscovery_ReturnsSendAndSubmit()
    {
        var session = CreateSession();
        var fsm = new AgentStateMachine(session);

        var actions = fsm.GetAllowedActions();

        Assert.Contains(AgentAction.ContinueConversation, actions);
        Assert.Contains(AgentAction.Submit, actions);
        Assert.DoesNotContain(AgentAction.Approve, actions);
    }

    [Fact]
    public void GetAllowedActions_InSubmit_ReturnsApproveAndRevise()
    {
        var session = CreateSession();
        var fsm = new AgentStateMachine(session);
        var draft = new ConstraintDraft();
        draft.AddHardConstraint("avoid_weekday", "Friday");
        fsm.RequestSubmit(draft);

        var actions = fsm.GetAllowedActions();

        Assert.Contains(AgentAction.Approve, actions);
        Assert.Contains(AgentAction.Revise, actions);
        Assert.DoesNotContain(AgentAction.ContinueConversation, actions);
    }

    [Fact]
    public void GetAllowedActions_InApproved_ReturnsEmpty()
    {
        var session = CreateSession();
        var fsm = new AgentStateMachine(session);
        var draft = new ConstraintDraft();
        draft.AddHardConstraint("avoid_weekday", "Friday");
        fsm.RequestSubmit(draft);
        fsm.Approve();

        var actions = fsm.GetAllowedActions();

        Assert.Empty(actions);
    }
}
