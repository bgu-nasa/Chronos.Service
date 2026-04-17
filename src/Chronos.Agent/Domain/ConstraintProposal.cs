using Chronos.Domain.Schedule;

namespace Chronos.Agent.Domain;

/// <summary>
/// Immutable approved constraint set, ready to be submitted to Chronos data layer.
/// Created from an approved ConstraintDraft.
/// </summary>
public sealed class ConstraintProposal
{
    public Guid UserId { get; }
    public Guid OrganizationId { get; }
    public Guid SchedulingPeriodId { get; }
    public IReadOnlyList<UserConstraint> Constraints { get; }
    public IReadOnlyList<UserPreference> Preferences { get; }
    public DateTime ApprovedAt { get; }

    public ConstraintProposal(
        Guid userId,
        Guid organizationId,
        Guid schedulingPeriodId,
        IReadOnlyList<UserConstraint> constraints,
        IReadOnlyList<UserPreference> preferences)
    {
        UserId = userId;
        OrganizationId = organizationId;
        SchedulingPeriodId = schedulingPeriodId;
        Constraints = constraints;
        Preferences = preferences;
        ApprovedAt = DateTime.UtcNow;
    }
}
