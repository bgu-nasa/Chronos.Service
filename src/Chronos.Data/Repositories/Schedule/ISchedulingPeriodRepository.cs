using Chronos.Domain.Schedule;

namespace Chronos.Data.Repositories.Schedule;

public interface ISchedulingPeriodRepository
{
    Task<SchedulingPeriod?> GetByIdAsync(Guid id);
    
    Task<SchedulingPeriod?> GetByNameAsync(string name);

    Task<(List<SchedulingPeriod> Items, int TotalCount)> GetAllAsync(int page, int pageSize);

    Task AddAsync(SchedulingPeriod schedulingPeriod);

    Task UpdateAsync(SchedulingPeriod schedulingPeriod);

    Task DeleteAsync(SchedulingPeriod schedulingPeriod);

    Task<bool> ExistsAsync(Guid id);

}

