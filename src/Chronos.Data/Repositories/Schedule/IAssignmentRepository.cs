using Chronos.Domain.Schedule;

namespace Chronos.Data.Repositories.Schedule;

public interface IAssignmentRepository
{
    Task<Assignment?> GetByIdAsync(Guid id);

    Task<(List<Assignment> Items, int TotalCount)> GetAllAsync(int page, int pageSize);

    Task<(List<Assignment> Items, int TotalCount)> GetBySlotIdAsync(Guid slotId, int page, int pageSize);
    Task<(List<Assignment> Items, int TotalCount)> GetByActivityIdAsync(Guid activityId, int page, int pageSize);
    Task<Assignment?> GetBySlotIdAndResourceIdAsync(Guid slotId, Guid resourceId);

    Task< List<Assignment>> GetByResourceIdAsync(Guid resourceId);
    
    Task AddAsync(Assignment assignment);

    Task UpdateAsync(Assignment assignment);

    Task DeleteAsync(Assignment assignment);

    Task<bool> ExistsAsync(Guid id);
}
