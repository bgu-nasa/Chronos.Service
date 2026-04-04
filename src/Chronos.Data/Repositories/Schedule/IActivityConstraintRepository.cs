using Chronos.Domain.Schedule;

namespace Chronos.Data.Repositories.Schedule;

public interface IActivityConstraintRepository
{
    Task<ActivityConstraint?> GetByIdAsync(Guid id);

    Task<List<ActivityConstraint>> GetAllAsync();

    Task<List<ActivityConstraint>> GetByActivityIdAsync(Guid activityId);

    Task AddAsync(ActivityConstraint constraint);

    Task UpdateAsync(ActivityConstraint constraint);

    Task DeleteAsync(ActivityConstraint constraint);
    Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);

    Task<bool> ExistsAsync(Guid id);
}
