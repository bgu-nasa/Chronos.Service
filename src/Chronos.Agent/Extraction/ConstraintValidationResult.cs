namespace Chronos.Agent.Extraction;

public sealed class ConstraintValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = [];
}
