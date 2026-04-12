using Chronos.Data.Repositories.Common;
using Chronos.Domain.Schedule;

namespace Chronos.Data.Repositories.Schedule;

public interface IActivityConstraintRepository : IDepartmentScopedRepository
{
    Task<ActivityConstraint?> GetByIdAsync(Guid id);

    Task<List<ActivityConstraint>> GetAllAsync();

    Task<List<ActivityConstraint>> GetByActivityIdAsync(Guid activityId);

    Task AddAsync(ActivityConstraint constraint);

    Task UpdateAsync(ActivityConstraint constraint);

    Task DeleteAsync(ActivityConstraint constraint);
    Task<bool> ExistsAsync(Guid id);
}
