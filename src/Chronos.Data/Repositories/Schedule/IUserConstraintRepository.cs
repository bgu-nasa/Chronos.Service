using Chronos.Data.Repositories.Common;
using Chronos.Domain.Schedule;

namespace Chronos.Data.Repositories.Schedule;
public interface IUserConstraintRepository : IOrganizationScopedRepository
{
    Task<UserConstraint?> GetByIdAsync(Guid id);

    Task<List<UserConstraint>> GetAllAsync();

    Task<List<UserConstraint>> GetByUserPeriodAsync(Guid userId, Guid schedulingPeriodId);

    Task<List<UserConstraint>> GetByUserIdAsync(Guid userId);

    Task<List<UserConstraint>> GetBySchedulingPeriodIdAsync(Guid schedulingPeriodId);

    Task AddAsync(UserConstraint constraint);

    Task UpdateAsync(UserConstraint constraint);

    Task DeleteAsync(UserConstraint constraint);
    Task<bool> ExistsAsync(Guid id);
}
