namespace Chronos.Agent.Submission;

/// <summary>
/// Thin interface mirroring the constraint creation method from IUserConstraintService.
/// This allows the agent library to depend on an abstraction rather than
/// directly referencing the MainApi service layer.
/// The MainApi registers its UserConstraintService as this interface at bootstrap.
/// </summary>
public interface IAgentConstraintService
{
    Task<Guid> CreateUserConstraintAsync(
        Guid organizationId, Guid userId, Guid schedulingPeriodId,
        string key, string value, int? weekNum = null);
}

/// <summary>
/// Thin interface mirroring the preference creation method from IUserPreferenceService.
/// </summary>
public interface IAgentPreferenceService
{
    Task<Guid> CreateUserPreferenceAsync(
        Guid organizationId, Guid userId, Guid schedulingPeriodId,
        string key, string value);
}
