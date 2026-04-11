using Chronos.Data.Repositories.Common;
using Chronos.Domain.Schedule;

namespace Chronos.Data.Repositories.Schedule;

public interface IUserPreferenceRepository : IOrganizationScopedRepository
{    
    Task<UserPreference?> GetByIdAsync(Guid id);

    Task<List<UserPreference>> GetAllAsync();

    Task<List<UserPreference>> GetByUserPeriodAsync
        (Guid userId, Guid schedulingPeriodId);

    Task<List<UserPreference>> GetByUserIdAsync(Guid userId);

    Task<List<UserPreference>> GetBySchedulingPeriodIdAsync(Guid schedulingPeriodId);

    Task AddAsync(UserPreference preference);

    Task UpdateAsync(UserPreference preference);

    Task DeleteAsync(UserPreference preference);
    Task<bool> ExistsAsync(Guid id);
}

