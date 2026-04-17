using Chronos.Agent.Domain;

namespace Chronos.Agent.Submission;

/// <summary>
/// Submitter that delegates to the existing Chronos service layer.
/// The services (IAgentConstraintService, IAgentPreferenceService) handle:
///   - Repository writes (UserConstraint / UserPreference)
///   - RabbitMQ message publishing (HandleConstraintChangeRequest)
///   - Validation (org exists, scheduling period valid)
/// 
/// This way the agent reuses existing infrastructure and doesn't duplicate
/// DB writes or queue publishing logic.
/// </summary>
public class ServiceBackedSubmitter : IAgentSubmitter
{
    private readonly IAgentConstraintService _constraintService;
    private readonly IAgentPreferenceService _preferenceService;

    public ServiceBackedSubmitter(
        IAgentConstraintService constraintService,
        IAgentPreferenceService preferenceService)
    {
        _constraintService = constraintService ?? throw new ArgumentNullException(nameof(constraintService));
        _preferenceService = preferenceService ?? throw new ArgumentNullException(nameof(preferenceService));
    }

    public async Task SubmitAsync(ConstraintProposal proposal, CancellationToken cancellationToken = default)
    {
        foreach (var constraint in proposal.Constraints)
        {
            await _constraintService.CreateUserConstraintAsync(
                proposal.OrganizationId,
                proposal.UserId,
                proposal.SchedulingPeriodId,
                constraint.Key,
                constraint.Value,
                constraint.WeekNum);
        }

        foreach (var preference in proposal.Preferences)
        {
            await _preferenceService.CreateUserPreferenceAsync(
                proposal.OrganizationId,
                proposal.UserId,
                proposal.SchedulingPeriodId,
                preference.Key,
                preference.Value);
        }
    }
}
