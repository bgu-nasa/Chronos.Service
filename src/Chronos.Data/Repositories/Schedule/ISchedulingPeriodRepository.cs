using Chronos.Domain.Schedule;

namespace Chronos.Data.Repositories.Schedule;

public interface ISchedulingPeriodRepository
{
    Task<SchedulingPeriod?> GetByIdAsync(Guid id);
    
    Task<SchedulingPeriod?> GetByNameAsync(string name);

    Task<List<SchedulingPeriod>> GetAllAsync();

    Task AddAsync(SchedulingPeriod schedulingPeriod);

    Task UpdateAsync(SchedulingPeriod schedulingPeriod);

    Task DeleteAsync(SchedulingPeriod schedulingPeriod);
    Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);

    Task<bool> ExistsAsync(Guid id);

}

