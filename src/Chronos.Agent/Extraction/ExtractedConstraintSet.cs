namespace Chronos.Agent.Extraction;

public sealed class ExtractedConstraintSet
{
    public List<(string Key, string Value)> HardConstraints { get; set; } = [];
    public List<(string Key, string Value)> SoftPreferences { get; set; } = [];
}
