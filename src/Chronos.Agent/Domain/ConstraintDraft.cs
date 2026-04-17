namespace Chronos.Agent.Domain;

public record DraftConstraint(string Key, string Value);
public record DraftPreference(string Key, string Value);

/// <summary>
/// Mutable draft of constraints and preferences being built during conversation.
/// Only becomes immutable once approved and converted to a ConstraintProposal.
/// </summary>
public class ConstraintDraft
{
    private readonly List<DraftConstraint> _hardConstraints = new();
    private readonly List<DraftPreference> _softPreferences = new();

    public IReadOnlyList<DraftConstraint> HardConstraints => _hardConstraints.AsReadOnly();
    public IReadOnlyList<DraftPreference> SoftPreferences => _softPreferences.AsReadOnly();

    public void AddHardConstraint(string key, string value)
        => _hardConstraints.Add(new DraftConstraint(key, value));

    public void AddSoftPreference(string key, string value)
        => _softPreferences.Add(new DraftPreference(key, value));

    public void Clear()
    {
        _hardConstraints.Clear();
        _softPreferences.Clear();
    }
}
