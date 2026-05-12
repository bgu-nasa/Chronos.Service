namespace Chronos.Agent.Extraction;

/// <summary>
/// Catalog of constraint and preference keys the Chronos engine actually does something
/// with when they arrive via <c>UserConstraint</c> / <c>UserPreference</c> — i.e. the same
/// path the agent uses (<c>POST /api/schedule/constraints/userConstraint</c> /
/// <c>preferenceConstraint</c>).
///
/// Sources of truth, traced through the code (do not extend without checking):
///
/// - <b>Hard constraint keys</b> (UserConstraint): only what
///   <c>ActivityConstraintProcessor.ProcessConstraintAsync</c> handles for user-scoped
///   constraints. There are no <c>IConstraintHandler</c> implementations registered, so
///   only the built-in fallback branch fires — <c>forbidden_timerange</c>. Every other
///   key produces a "No handler found for constraint key… Skipping" warning and silently
///   does nothing. The activity-only validators (<c>time_range</c>,
///   <c>required_capacity</c>, <c>compatible_resource_types</c>, etc.) only run on
///   <c>ActivityConstraint</c> records loaded by <c>IActivityConstraintRepository</c>;
///   they are deliberately excluded here.
///
/// - <b>Soft preference keys</b> (UserPreference): every <c>case</c> branch of
///   <c>PreferenceWeightedRanker.CandidateMatchesPreference</c>. Anything outside this
///   set falls through to the <c>_ =&gt; false</c> default and contributes nothing.
/// </summary>
public static class KnownConstraintKeys
{
    public static readonly IReadOnlySet<string> HardConstraintKeys = new HashSet<string>
    {
        "forbidden_timerange"
    };

    public static readonly IReadOnlySet<string> SoftPreferenceKeys = new HashSet<string>
    {
        "preferred_weekday",
        "preferred_weekdays",
        "avoid_weekday",
        "preferred_time_morning",
        "preferred_time_afternoon",
        "preferred_time_evening",
        "preferred_timerange"
    };

    public static readonly IReadOnlySet<string> AllKeys =
        new HashSet<string>(HardConstraintKeys.Concat(SoftPreferenceKeys));

    public static bool IsValid(string key) => AllKeys.Contains(key);
}
