using Chronos.Domain.Schedule;

namespace Chronos.Agent.Domain;

public sealed class ConstraintProposal
{
    public Guid UserId { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid SchedulingPeriodId { get; init; }
    public IReadOnlyList<UserConstraint> Constraints { get; init; } = [];
    public IReadOnlyList<UserPreference> Preferences { get; init; } = [];
    public DateTime ApprovedAtUtc { get; init; }

    public static ConstraintProposal FromDraft(Guid userId, Guid organizationId, Guid schedulingPeriodId, ConstraintDraft draft)
    {
        var constraints = draft.HardConstraints
            .Select(c => new UserConstraint
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrganizationId = organizationId,
                SchedulingPeriodId = schedulingPeriodId,
                Key = c.Key,
                Value = c.Value
            })
            .ToList();

        var preferences = draft.SoftPreferences
            .Select(p => new UserPreference
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrganizationId = organizationId,
                SchedulingPeriodId = schedulingPeriodId,
                Key = p.Key,
                Value = p.Value
            })
            .ToList();

        return new ConstraintProposal
        {
            UserId = userId,
            OrganizationId = organizationId,
            SchedulingPeriodId = schedulingPeriodId,
            Constraints = constraints,
            Preferences = preferences,
            ApprovedAtUtc = DateTime.UtcNow
        };
    }
}
