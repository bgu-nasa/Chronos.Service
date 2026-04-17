using Chronos.Agent.Domain;

namespace Chronos.Agent.Submission;

/// <summary>
/// Abstraction for committing an approved constraint proposal.
/// Implementations handle writing to the Chronos data layer and triggering
/// downstream processing (e.g. RabbitMQ messages for online scheduling).
/// </summary>
public interface IAgentSubmitter
{
    Task SubmitAsync(ConstraintProposal proposal, CancellationToken cancellationToken = default);
}
