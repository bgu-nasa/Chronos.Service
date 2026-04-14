using Chronos.Agent;
using Chronos.Agent.Conversation;
using Chronos.MainApi.Agent.Contracts;
using Chronos.MainApi.Schedule.Services;

namespace Chronos.MainApi.Agent.Services;

public sealed class AgentService(
    AgentOrchestrator orchestrator,
    IUserConstraintService userConstraintService,
    IUserPreferenceService userPreferenceService) : IAgentService
{
    public async Task<AgentTurnResponse> StartSessionAsync(Guid organizationId, Guid userId, Guid schedulingPeriodId, CancellationToken cancellationToken = default)
    {
        var session = await orchestrator.StartSessionAsync(userId, organizationId, schedulingPeriodId, cancellationToken);
        return new AgentTurnResponse(
            session.Id.ToString(),
            session.State.ToString(),
            AgentMode.Conversation.ToString(),
            "Session started. Share your constraints.",
            [],
            [],
            []);
    }

    public async Task<AgentTurnResponse> SendMessageAsync(Guid organizationId, Guid userId, Guid sessionId, string message, CancellationToken cancellationToken = default)
    {
        await EnsureOwnershipAsync(organizationId, userId, sessionId, cancellationToken);
        var result = await orchestrator.ProcessMessageAsync(sessionId, message, cancellationToken);
        return MapTurn(result);
    }

    public async Task<AgentTurnResponse> ReviseAsync(Guid organizationId, Guid userId, Guid sessionId, string message, CancellationToken cancellationToken = default)
    {
        await EnsureOwnershipAsync(organizationId, userId, sessionId, cancellationToken);
        var result = await orchestrator.ReviseAsync(sessionId, message, cancellationToken);
        return MapTurn(result);
    }

    public async Task<AgentTurnResponse> GetSessionAsync(Guid organizationId, Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await EnsureOwnershipAsync(organizationId, userId, sessionId, cancellationToken);

        return new AgentTurnResponse(
            session.Id.ToString(),
            session.State.ToString(),
            session.State == AgentState.Submit ? AgentMode.Submit.ToString() : AgentMode.Conversation.ToString(),
            session.State == AgentState.Submit ? "Draft is ready for approval." : "Session loaded.",
            session.Draft?.HardConstraints.Select(c => new AgentDraftItemResponse(c.Key, c.Value)).ToList() ?? [],
            session.Draft?.SoftPreferences.Select(p => new AgentDraftItemResponse(p.Key, p.Value)).ToList() ?? [],
            []);
    }

    public async Task<ApproveAgentSessionResponse> ApproveAsync(Guid organizationId, Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        await EnsureOwnershipAsync(organizationId, userId, sessionId, cancellationToken);
        var proposal = await orchestrator.ApproveAsync(sessionId, cancellationToken);

        foreach (var constraint in proposal.Constraints)
        {
            await userConstraintService.CreateUserConstraintAsync(
                organizationId,
                userId,
                proposal.SchedulingPeriodId,
                constraint.Key,
                constraint.Value,
                constraint.WeekNum);
        }

        foreach (var preference in proposal.Preferences)
        {
            await userPreferenceService.CreateUserPreferenceAsync(
                organizationId,
                userId,
                proposal.SchedulingPeriodId,
                preference.Key,
                preference.Value);
        }

        return new ApproveAgentSessionResponse(
            sessionId.ToString(),
            proposal.Constraints.Count,
            proposal.Preferences.Count);
    }

    private async Task<ConversationSession> EnsureOwnershipAsync(Guid organizationId, Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await orchestrator.GetSessionAsync(sessionId, cancellationToken);
        if (session.OrganizationId != organizationId || session.UserId != userId)
        {
            throw new UnauthorizedAccessException("Session ownership mismatch.");
        }

        return session;
    }

    private static AgentTurnResponse MapTurn(AgentTurnResult result)
    {
        return new AgentTurnResponse(
            result.SessionId.ToString(),
            result.State.ToString(),
            result.Mode.ToString(),
            result.Message,
            result.Draft?.HardConstraints.Select(c => new AgentDraftItemResponse(c.Key, c.Value)).ToList() ?? [],
            result.Draft?.SoftPreferences.Select(p => new AgentDraftItemResponse(p.Key, p.Value)).ToList() ?? [],
            result.Errors);
    }
}
