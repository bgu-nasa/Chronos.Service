using System.Text.Json;
using Chronos.Agent;
using Chronos.Agent.Conversation;
using Chronos.Agent.Domain;
using Chronos.Agent.Extraction;
using Chronos.Agent.Submission;
using Moq;

namespace Chronos.Tests.Agent;

public class AgentOrchestratorTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _periodId = Guid.NewGuid();

    private Mock<ILlmAdapter> _llmAdapter = null!;
    private Mock<IAgentSubmitter> _submitter = null!;
    private IConversationStore _store = null!;
    private AgentOrchestrator _orchestrator = null!;

    private void SetupOrchestrator(string? conversationReply = null, string? extractionJson = null)
    {
        _llmAdapter = new Mock<ILlmAdapter>();
        _submitter = new Mock<IAgentSubmitter>();
        _store = new InMemoryConversationStore();

        // Default: conversation reply
        _llmAdapter
            .Setup(a => a.ChatAsync(
                It.Is<IReadOnlyList<ChatMessage>>(msgs =>
                    msgs.Any(m => m.Content.Contains("scheduling assistant"))),
                It.Is<LlmOptions?>(o => o == null || !o.JsonMode),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse(conversationReply ?? "Got it, you want to avoid Fridays."));

        // Extraction reply (JSON mode)
        var json = extractionJson ?? JsonSerializer.Serialize(new
        {
            hardConstraints = new[] { new { key = "avoid_weekday", value = "Friday" } },
            softPreferences = new[] { new { key = "preferred_weekday", value = "Monday" } }
        });
        _llmAdapter
            .Setup(a => a.ChatAsync(
                It.Is<IReadOnlyList<ChatMessage>>(msgs =>
                    msgs.Any(m => m.Content.Contains("hardConstraints"))),
                It.Is<LlmOptions?>(o => o != null && o.JsonMode),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse(json));

        _submitter
            .Setup(s => s.SubmitAsync(It.IsAny<ConstraintProposal>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var extractor = new ConstraintExtractor(_llmAdapter.Object);
        _orchestrator = new AgentOrchestrator(_llmAdapter.Object, extractor, _submitter.Object, _store);
    }

    [Fact]
    public async Task StartSession_CreatesSession_ReturnsId()
    {
        SetupOrchestrator();

        var sessionId = await _orchestrator.StartSessionAsync(_userId, _orgId, _periodId);

        Assert.NotEqual(Guid.Empty, sessionId);
        var session = await _store.GetAsync(sessionId);
        Assert.NotNull(session);
        Assert.Equal(AgentState.Discovery, session!.State);
    }

    [Fact]
    public async Task SendMessage_InDiscovery_ReturnsConversationReply()
    {
        SetupOrchestrator(conversationReply: "Sure, avoiding Fridays!");
        var sessionId = await _orchestrator.StartSessionAsync(_userId, _orgId, _periodId);

        var result = await _orchestrator.SendMessageAsync(sessionId, "I can't work Fridays");

        Assert.Equal("Sure, avoiding Fridays!", result.AssistantMessage);
        Assert.Equal(AgentState.Discovery, result.State);
        Assert.Contains(AgentAction.ContinueConversation, result.AllowedActions);
    }

    [Fact]
    public async Task SendMessage_StoresMessagesInSession()
    {
        SetupOrchestrator();
        var sessionId = await _orchestrator.StartSessionAsync(_userId, _orgId, _periodId);

        await _orchestrator.SendMessageAsync(sessionId, "I prefer mornings");

        var session = await _store.GetAsync(sessionId);
        // system + user + assistant = 3
        Assert.True(session!.Messages.Count >= 2);
        Assert.Equal("user", session.Messages[^2].Role);
        Assert.Equal("assistant", session.Messages[^1].Role);
    }

    [Fact]
    public async Task RequestSubmit_ExtractsAndTransitionsToSubmit()
    {
        SetupOrchestrator();
        var sessionId = await _orchestrator.StartSessionAsync(_userId, _orgId, _periodId);
        await _orchestrator.SendMessageAsync(sessionId, "I can't work Fridays and prefer Mondays");

        var result = await _orchestrator.RequestSubmitAsync(sessionId);

        Assert.Equal(AgentState.Submit, result.State);
        Assert.NotNull(result.Draft);
        Assert.Single(result.Draft!.HardConstraints);
        Assert.Single(result.Draft!.SoftPreferences);
        Assert.Contains(AgentAction.Approve, result.AllowedActions);
        Assert.Contains(AgentAction.Revise, result.AllowedActions);
    }

    [Fact]
    public async Task Approve_SubmitsProposal_TransitionsToApproved()
    {
        SetupOrchestrator();
        var sessionId = await _orchestrator.StartSessionAsync(_userId, _orgId, _periodId);
        await _orchestrator.SendMessageAsync(sessionId, "I can't work Fridays");
        await _orchestrator.RequestSubmitAsync(sessionId);

        var result = await _orchestrator.ApproveAsync(sessionId);

        Assert.Equal(AgentState.Approved, result.State);
        _submitter.Verify(
            s => s.SubmitAsync(It.Is<ConstraintProposal>(p =>
                p.Constraints.Count == 1 && p.Preferences.Count == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Revise_TransitionsToRevision_CanResendMessage()
    {
        SetupOrchestrator();
        var sessionId = await _orchestrator.StartSessionAsync(_userId, _orgId, _periodId);
        await _orchestrator.SendMessageAsync(sessionId, "I can't work Fridays");
        await _orchestrator.RequestSubmitAsync(sessionId);

        var result = await _orchestrator.ReviseAsync(sessionId);

        Assert.Equal(AgentState.Revision, result.State);
        Assert.Contains(AgentAction.ContinueConversation, result.AllowedActions);
    }

    [Fact]
    public async Task SendMessage_InvalidSessionId_Throws()
    {
        SetupOrchestrator();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _orchestrator.SendMessageAsync(Guid.NewGuid(), "test"));
    }

    [Fact]
    public async Task Approve_NotInSubmitState_Throws()
    {
        SetupOrchestrator();
        var sessionId = await _orchestrator.StartSessionAsync(_userId, _orgId, _periodId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _orchestrator.ApproveAsync(sessionId));
    }
}
