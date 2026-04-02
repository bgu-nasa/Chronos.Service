using Chronos.Domain.Schedule;

namespace Chronos.Data.Repositories.Schedule;

public interface ISlotRepository
{
    Task<Slot?> GetByIdAsync(Guid id);

    Task<(List<Slot> Items, int TotalCount)> GetBySchedulingPeriodIdAsync(Guid schedulingPeriodId, int page, int pageSize);

    Task<(List<Slot> Items, int TotalCount)> GetAllAsync(int page, int pageSize);

    Task AddAsync(Slot slot);

    Task UpdateAsync(Slot slot);

    Task DeleteAsync(Slot slot);

    Task<bool> ExistsAsync(Guid id);
}

