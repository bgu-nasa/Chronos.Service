using Chronos.MainApi.Agent.Contracts;

namespace Chronos.MainApi.Agent.Services;

public interface IAgentService
{
    Task<AgentTurnResponse> StartSessionAsync(Guid organizationId, Guid userId, Guid schedulingPeriodId, CancellationToken cancellationToken = default);
    Task<AgentTurnResponse> SendMessageAsync(Guid organizationId, Guid userId, Guid sessionId, string message, CancellationToken cancellationToken = default);
    Task<AgentTurnResponse> ReviseAsync(Guid organizationId, Guid userId, Guid sessionId, string message, CancellationToken cancellationToken = default);
    Task<AgentTurnResponse> GetSessionAsync(Guid organizationId, Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<ApproveAgentSessionResponse> ApproveAsync(Guid organizationId, Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
}
