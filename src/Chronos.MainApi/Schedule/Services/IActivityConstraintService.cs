using Chronos.Domain.Schedule;

namespace Chronos.MainApi.Schedule.Services;

public interface IActivityConstraintService
{
    Task<Guid> CreateActivityConstraintAsync(Guid organizationId, Guid activityId, string key, string value, int? weekNum = null);
    Task<ActivityConstraint> GetActivityConstraintByIdAsync(Guid organizationId, Guid activityConstraintId);
    Task<List<ActivityConstraint>> GetAllActivityConstraintsAsync(Guid organizationId);
    Task<List<ActivityConstraint>> GetByActivityIdAsync(Guid organizationId, Guid activityId);
    Task<ActivityConstraint> UpdateActivityConstraintAsync(Guid organizationId, Guid activityConstraintId, string key,
        string value, int? weekNum = null);
    Task DeleteActivityConstraintAsync(Guid organizationId, Guid activityConstraintId);
}