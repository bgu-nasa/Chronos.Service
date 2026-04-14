using Chronos.Agent.Conversation;
using Chronos.Agent.Domain;

namespace Chronos.Tests.Agent;

public class ChatMessageTests
{
    [Fact]
    public void Create_UserMessage_SetsRoleAndContent()
    {
        var msg = new ChatMessage("user", "I prefer mornings");

        Assert.Equal("user", msg.Role);
        Assert.Equal("I prefer mornings", msg.Content);
    }

    [Fact]
    public void Create_SystemMessage_SetsRoleAndContent()
    {
        var msg = new ChatMessage("system", "You are a scheduling assistant.");

        Assert.Equal("system", msg.Role);
        Assert.Equal("You are a scheduling assistant.", msg.Content);
    }

    [Fact]
    public void Create_AssistantMessage_SetsRoleAndContent()
    {
        var msg = new ChatMessage("assistant", "Got it, avoiding Fridays.");

        Assert.Equal("assistant", msg.Role);
        Assert.Equal("Got it, avoiding Fridays.", msg.Content);
    }
}

public class ConstraintDraftTests
{
    [Fact]
    public void NewDraft_HasEmptyLists()
    {
        var draft = new ConstraintDraft();

        Assert.Empty(draft.HardConstraints);
        Assert.Empty(draft.SoftPreferences);
    }

    [Fact]
    public void AddHardConstraint_AppearsInList()
    {
        var draft = new ConstraintDraft();

        draft.AddHardConstraint("avoid_weekday", "Friday");

        Assert.Single(draft.HardConstraints);
        Assert.Equal("avoid_weekday", draft.HardConstraints[0].Key);
        Assert.Equal("Friday", draft.HardConstraints[0].Value);
    }

    [Fact]
    public void AddSoftPreference_AppearsInList()
    {
        var draft = new ConstraintDraft();

        draft.AddSoftPreference("preferred_weekday", "Monday");

        Assert.Single(draft.SoftPreferences);
        Assert.Equal("preferred_weekday", draft.SoftPreferences[0].Key);
        Assert.Equal("Monday", draft.SoftPreferences[0].Value);
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        var draft = new ConstraintDraft();
        draft.AddHardConstraint("avoid_weekday", "Friday");
        draft.AddSoftPreference("preferred_weekday", "Monday");

        draft.Clear();

        Assert.Empty(draft.HardConstraints);
        Assert.Empty(draft.SoftPreferences);
    }
}

public class ConversationSessionTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _periodId = Guid.NewGuid();

    private ConversationSession CreateSession() =>
        new(_userId, _orgId, _periodId);

    [Fact]
    public void NewSession_StartsInDiscoveryState()
    {
        var session = CreateSession();

        Assert.Equal(AgentState.Discovery, session.State);
    }

    [Fact]
    public void NewSession_HasEmptyMessages()
    {
        var session = CreateSession();

        Assert.Empty(session.Messages);
    }

    [Fact]
    public void NewSession_HasNoDraft()
    {
        var session = CreateSession();

        Assert.Null(session.Draft);
    }

    [Fact]
    public void NewSession_HasGeneratedId()
    {
        var session = CreateSession();

        Assert.NotEqual(Guid.Empty, session.Id);
    }

    [Fact]
    public void AddUserMessage_AppendsToMessages()
    {
        var session = CreateSession();

        session.AddUserMessage("I prefer mornings");

        Assert.Single(session.Messages);
        Assert.Equal("user", session.Messages[0].Role);
        Assert.Equal("I prefer mornings", session.Messages[0].Content);
    }

    [Fact]
    public void AddAssistantMessage_AppendsToMessages()
    {
        var session = CreateSession();

        session.AddAssistantMessage("Got it!");

        Assert.Single(session.Messages);
        Assert.Equal("assistant", session.Messages[0].Role);
    }

    [Fact]
    public void AddSystemMessage_AppendsToMessages()
    {
        var session = CreateSession();

        session.AddSystemMessage("You are a scheduling assistant.");

        Assert.Single(session.Messages);
        Assert.Equal("system", session.Messages[0].Role);
    }

    [Fact]
    public void TransitionTo_Drafting_FromDiscovery_Succeeds()
    {
        var session = CreateSession();

        session.TransitionTo(AgentState.Drafting);

        Assert.Equal(AgentState.Drafting, session.State);
    }

    [Fact]
    public void TransitionTo_Submit_FromDrafting_Succeeds()
    {
        var session = CreateSession();
        session.TransitionTo(AgentState.Drafting);

        session.TransitionTo(AgentState.Submit);

        Assert.Equal(AgentState.Submit, session.State);
    }

    [Fact]
    public void TransitionTo_Revision_FromSubmit_Succeeds()
    {
        var session = CreateSession();
        session.TransitionTo(AgentState.Drafting);
        session.TransitionTo(AgentState.Submit);

        session.TransitionTo(AgentState.Revision);

        Assert.Equal(AgentState.Revision, session.State);
    }

    [Fact]
    public void TransitionTo_Approved_FromSubmit_Succeeds()
    {
        var session = CreateSession();
        session.TransitionTo(AgentState.Drafting);
        session.TransitionTo(AgentState.Submit);

        session.TransitionTo(AgentState.Approved);

        Assert.Equal(AgentState.Approved, session.State);
    }

    [Fact]
    public void TransitionTo_Submit_FromRevision_Succeeds()
    {
        var session = CreateSession();
        session.TransitionTo(AgentState.Drafting);
        session.TransitionTo(AgentState.Submit);
        session.TransitionTo(AgentState.Revision);

        session.TransitionTo(AgentState.Submit);

        Assert.Equal(AgentState.Submit, session.State);
    }

    [Fact]
    public void TransitionTo_Approved_FromDiscovery_Throws()
    {
        var session = CreateSession();

        Assert.Throws<InvalidOperationException>(
            () => session.TransitionTo(AgentState.Approved));
    }

    [Fact]
    public void TransitionTo_Discovery_FromApproved_Throws()
    {
        var session = CreateSession();
        session.TransitionTo(AgentState.Drafting);
        session.TransitionTo(AgentState.Submit);
        session.TransitionTo(AgentState.Approved);

        Assert.Throws<InvalidOperationException>(
            () => session.TransitionTo(AgentState.Discovery));
    }

    [Fact]
    public void TransitionTo_Discovery_FromSubmit_Throws()
    {
        var session = CreateSession();
        session.TransitionTo(AgentState.Drafting);
        session.TransitionTo(AgentState.Submit);

        Assert.Throws<InvalidOperationException>(
            () => session.TransitionTo(AgentState.Discovery));
    }

    [Fact]
    public void SetDraft_StoresDraftOnSession()
    {
        var session = CreateSession();
        var draft = new ConstraintDraft();
        draft.AddHardConstraint("avoid_weekday", "Friday");

        session.SetDraft(draft);

        Assert.NotNull(session.Draft);
        Assert.Single(session.Draft!.HardConstraints);
    }

    [Fact]
    public void Session_PreservesUserContext()
    {
        var session = CreateSession();

        Assert.Equal(_userId, session.UserId);
        Assert.Equal(_orgId, session.OrganizationId);
        Assert.Equal(_periodId, session.SchedulingPeriodId);
    }
}
