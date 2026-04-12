using Chronos.Data.Repositories.Common;
using Chronos.Domain.Schedule;

namespace Chronos.Data.Repositories.Schedule;

public interface IAssignmentRepository : IDepartmentScopedRepository
{
    Task<Assignment?> GetByIdAsync(Guid id);

    Task<List<Assignment>> GetAllAsync(AssignmentQuery? query = null);

    Task<List<Assignment>> GetBySlotIdAsync(Guid slotId);
    Task<List<Assignment>> GetByActivityIdAsync(Guid activityId);
    Task<Assignment?> GetBySlotIdAndResourceIdAsync(Guid slotId, Guid resourceId);

    Task<List<Assignment>> GetByResourceIdAsync(Guid resourceId);
    Task<List<Assignment>> GetBySchedulingPeriodIdAsync(Guid schedulingPeriodId);

    Task AddAsync(Assignment assignment);

    Task UpdateAsync(Assignment assignment);

    Task DeleteAsync(Assignment assignment);
    Task DeleteBySchedulingPeriodIdAsync(Guid schedulingPeriodId);
    Task<bool> ExistsAsync(Guid id);
}
