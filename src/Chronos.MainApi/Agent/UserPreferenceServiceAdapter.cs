using Chronos.Agent.Submission;
using Chronos.MainApi.Schedule.Services;

namespace Chronos.MainApi.Agent;

/// <summary>
/// Adapts the existing IUserPreferenceService to the agent's IAgentPreferenceService interface.
/// </summary>
public class UserPreferenceServiceAdapter : IAgentPreferenceService
{
    private readonly IUserPreferenceService _inner;

    public UserPreferenceServiceAdapter(IUserPreferenceService inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public Task<Guid> CreateUserPreferenceAsync(
        Guid organizationId, Guid userId, Guid schedulingPeriodId,
        string key, string value)
    {
        return _inner.CreateUserPreferenceAsync(organizationId, userId, schedulingPeriodId, key, value);
    }
}
