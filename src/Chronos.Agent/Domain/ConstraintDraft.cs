namespace Chronos.Agent.Domain;

public sealed class ConstraintDraft
{
    public List<DraftConstraint> HardConstraints { get; set; } = [];
    public List<DraftPreference> SoftPreferences { get; set; } = [];
}
