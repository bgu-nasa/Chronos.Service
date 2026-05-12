namespace Chronos.Agent.Domain;

/// <param name="WeekNum">
/// Optional ISO week number (1..53) when the constraint applies only to a single week
/// inside the scheduling period (one-time exception, e.g. "next Tuesday is my son's
/// birthday"). Null = recurring across the whole scheduling period.
/// Maps directly to <c>UserConstraint.WeekNum</c> on the engine side.
/// </param>
public record DraftConstraint(string Key, string Value, int? WeekNum = null);
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

    public void AddHardConstraint(string key, string value, int? weekNum = null)
        => _hardConstraints.Add(new DraftConstraint(key, value, weekNum));

    public void AddSoftPreference(string key, string value)
        => _softPreferences.Add(new DraftPreference(key, value));

    public void Clear()
    {
        _hardConstraints.Clear();
        _softPreferences.Clear();
    }
}
