using Chronos.Agent.Submission;
using Chronos.MainApi.Schedule.Services;

namespace Chronos.MainApi.Agent;

/// <summary>
/// Adapts the existing IUserConstraintService to the agent's IAgentConstraintService interface.
/// </summary>
public class UserConstraintServiceAdapter : IAgentConstraintService
{
    private readonly IUserConstraintService _inner;

    public UserConstraintServiceAdapter(IUserConstraintService inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public Task<Guid> CreateUserConstraintAsync(
        Guid organizationId, Guid userId, Guid schedulingPeriodId,
        string key, string value, int? weekNum = null)
    {
        return _inner.CreateUserConstraintAsync(organizationId, userId, schedulingPeriodId, key, value, weekNum);
    }
}
