using Chronos.Agent.Conversation;
using Chronos.Agent.Domain;
using Chronos.Agent.Extraction;

namespace Chronos.Agent;

public sealed class AgentOrchestrator(
    IConversationStore conversationStore,
    AgentStateMachine stateMachine,
    IConstraintExtractor extractor,
    ConstraintValidator validator)
{
    public async Task<ConversationSession> StartSessionAsync(Guid userId, Guid organizationId, Guid schedulingPeriodId, CancellationToken cancellationToken = default)
    {
        var session = new ConversationSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            SchedulingPeriodId = schedulingPeriodId,
            State = AgentState.Discovery
        };

        session.Messages.Add(new ChatMessage("assistant", "Tell me your scheduling constraints and preferences.", DateTime.UtcNow));
        return await conversationStore.CreateAsync(session, cancellationToken);
    }

    public async Task<ConversationSession> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await conversationStore.GetAsync(sessionId, cancellationToken);
        if (session == null)
        {
            throw new KeyNotFoundException($"Session {sessionId} not found.");
        }

        return session;
    }

    public async Task<AgentTurnResult> ProcessMessageAsync(Guid sessionId, string message, CancellationToken cancellationToken = default)
    {
        var session = await GetSessionAsync(sessionId, cancellationToken);

        if (session.State == AgentState.Approved)
        {
            throw new InvalidOperationException("This session is already approved.");
        }

        session.Messages.Add(new ChatMessage("user", message, DateTime.UtcNow));
        stateMachine.MoveToDrafting(session);

        var extracted = await extractor.ExtractAsync(session.Messages, cancellationToken);
        var validation = validator.Validate(extracted);

        if (!validation.IsValid)
        {
            session.Messages.Add(new ChatMessage("assistant", "I found issues with this input. Please revise your request.", DateTime.UtcNow));
            stateMachine.MoveToRevision(session);
            await conversationStore.SaveAsync(session, cancellationToken);

            return new AgentTurnResult
            {
                SessionId = session.Id,
                State = session.State,
                Mode = AgentMode.Conversation,
                Message = "Please revise your constraints/preferences.",
                Errors = validation.Errors
            };
        }

        session.Draft = new ConstraintDraft
        {
            HardConstraints = extracted.HardConstraints.Select(h => new DraftConstraint(h.Key, h.Value)).ToList(),
            SoftPreferences = extracted.SoftPreferences.Select(s => new DraftPreference(s.Key, s.Value)).ToList()
        };

        if (session.Draft.HardConstraints.Count == 0 && session.Draft.SoftPreferences.Count == 0)
        {
            session.Messages.Add(new ChatMessage("assistant", "I could not extract constraints. Please be more specific.", DateTime.UtcNow));
            session.State = AgentState.Discovery;
            await conversationStore.SaveAsync(session, cancellationToken);

            return new AgentTurnResult
            {
                SessionId = session.Id,
                State = session.State,
                Mode = AgentMode.Conversation,
                Message = "I could not extract constraints from your message."
            };
        }

        stateMachine.MoveToSubmit(session);
        session.Messages.Add(new ChatMessage("assistant", "Review this draft and approve or revise.", DateTime.UtcNow));
        await conversationStore.SaveAsync(session, cancellationToken);

        return new AgentTurnResult
        {
            SessionId = session.Id,
            State = session.State,
            Mode = AgentMode.Submit,
            Message = "Draft is ready for approval.",
            Draft = session.Draft
        };
    }

    public async Task<AgentTurnResult> ReviseAsync(Guid sessionId, string message, CancellationToken cancellationToken = default)
    {
        var session = await GetSessionAsync(sessionId, cancellationToken);
        if (session.State == AgentState.Submit)
        {
            stateMachine.MoveToRevision(session);
            await conversationStore.SaveAsync(session, cancellationToken);
        }

        return await ProcessMessageAsync(sessionId, message, cancellationToken);
    }

    public async Task<ConstraintProposal> ApproveAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await GetSessionAsync(sessionId, cancellationToken);

        if (session.Draft == null)
        {
            throw new InvalidOperationException("Cannot approve without a draft.");
        }

        stateMachine.MoveToApproved(session);
        session.Messages.Add(new ChatMessage("assistant", "Approved. Submitting your constraints.", DateTime.UtcNow));
        await conversationStore.SaveAsync(session, cancellationToken);

        return ConstraintProposal.FromDraft(
            session.UserId,
            session.OrganizationId,
            session.SchedulingPeriodId,
            session.Draft);
    }
}
