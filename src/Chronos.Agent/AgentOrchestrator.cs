using Chronos.Agent.Conversation;
using Chronos.Agent.Domain;
using Chronos.Agent.Extraction;
using Chronos.Agent.Submission;
using Chronos.Domain.Schedule;

namespace Chronos.Agent;

/// <summary>
/// Response returned by the orchestrator for each user interaction.
/// </summary>
public record AgentResponse(
    AgentState State,
    string? AssistantMessage,
    ConstraintDraft? Draft,
    IReadOnlySet<AgentAction> AllowedActions);

/// <summary>
/// Main entry point for the Chronos conversational agent.
/// Coordinates session management, LLM calls, extraction, and submission.
/// </summary>
public class AgentOrchestrator
{
    private readonly ILlmAdapter _llmAdapter;
    private readonly ConstraintExtractor _extractor;
    private readonly IAgentSubmitter _submitter;
    private readonly IConversationStore _store;

    public AgentOrchestrator(
        ILlmAdapter llmAdapter,
        ConstraintExtractor extractor,
        IAgentSubmitter submitter,
        IConversationStore store)
    {
        _llmAdapter = llmAdapter ?? throw new ArgumentNullException(nameof(llmAdapter));
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        _submitter = submitter ?? throw new ArgumentNullException(nameof(submitter));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>Creates a new conversation session.</summary>
    public async Task<Guid> StartSessionAsync(
        Guid userId, Guid organizationId, Guid schedulingPeriodId)
    {
        var session = new ConversationSession(userId, organizationId, schedulingPeriodId);
        session.AddSystemMessage(PromptTemplates.ConversationSystemPrompt);
        await _store.SaveAsync(session);
        return session.Id;
    }

    /// <summary>Sends a user message and gets a conversational reply from the LLM.</summary>
    public async Task<AgentResponse> SendMessageAsync(
        Guid sessionId, string userMessage, CancellationToken cancellationToken = default)
    {
        var session = await GetSessionOrThrowAsync(sessionId);
        var fsm = new AgentStateMachine(session);

        fsm.ProcessUserMessage(userMessage);

        var response = await _llmAdapter.ChatAsync(session.Messages, null, cancellationToken);
        session.AddAssistantMessage(response.Content);

        await _store.SaveAsync(session);

        return new AgentResponse(
            session.State,
            response.Content,
            session.Draft,
            fsm.GetAllowedActions());
    }

    /// <summary>Triggers LLM extraction and transitions to Submit state.</summary>
    public async Task<AgentResponse> RequestSubmitAsync(
        Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await GetSessionOrThrowAsync(sessionId);
        var fsm = new AgentStateMachine(session);

        var draft = await _extractor.ExtractAsync(session.Messages, cancellationToken);
        fsm.RequestSubmit(draft);

        await _store.SaveAsync(session);

        return new AgentResponse(
            session.State,
            null,
            session.Draft,
            fsm.GetAllowedActions());
    }

    /// <summary>Approves the current draft, converts to proposal, and submits to Chronos.</summary>
    public async Task<AgentResponse> ApproveAsync(
        Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await GetSessionOrThrowAsync(sessionId);
        var fsm = new AgentStateMachine(session);

        fsm.Approve();

        if (session.Draft is not null)
        {
            var proposal = ConvertToProposal(session);
            await _submitter.SubmitAsync(proposal, cancellationToken);
        }

        await _store.SaveAsync(session);

        return new AgentResponse(
            session.State,
            null,
            session.Draft,
            fsm.GetAllowedActions());
    }

    /// <summary>Returns to Revision state for further edits.</summary>
    public async Task<AgentResponse> ReviseAsync(Guid sessionId)
    {
        var session = await GetSessionOrThrowAsync(sessionId);
        var fsm = new AgentStateMachine(session);

        fsm.RequestRevision();

        await _store.SaveAsync(session);

        return new AgentResponse(
            session.State,
            null,
            session.Draft,
            fsm.GetAllowedActions());
    }

    private async Task<ConversationSession> GetSessionOrThrowAsync(Guid sessionId)
    {
        var session = await _store.GetAsync(sessionId);
        if (session is null)
            throw new KeyNotFoundException($"Session {sessionId} not found.");
        return session;
    }

    private static ConstraintProposal ConvertToProposal(ConversationSession session)
    {
        var draft = session.Draft!;

        var constraints = draft.HardConstraints.Select(c => new UserConstraint
        {
            Id = Guid.NewGuid(),
            OrganizationId = session.OrganizationId,
            UserId = session.UserId,
            SchedulingPeriodId = session.SchedulingPeriodId,
            Key = c.Key,
            Value = c.Value
        }).ToList();

        var preferences = draft.SoftPreferences.Select(p => new UserPreference
        {
            Id = Guid.NewGuid(),
            OrganizationId = session.OrganizationId,
            UserId = session.UserId,
            SchedulingPeriodId = session.SchedulingPeriodId,
            Key = p.Key,
            Value = p.Value
        }).ToList();

        return new ConstraintProposal(
            session.UserId,
            session.OrganizationId,
            session.SchedulingPeriodId,
            constraints,
            preferences);
    }
}
