using Chronos.Domain.Schedule;

namespace Chronos.Data.Repositories.Schedule;

public interface IAssignmentRepository
{
    Task<Assignment?> GetByIdAsync(Guid id);

    Task<List<Assignment>> GetAllAsync(AssignmentQuery? query = null);

    Task<List<Assignment>> GetBySlotIdAsync(Guid slotId);
    Task<List<Assignment>> GetByActivityIdAsync(Guid activityId);
    Task<Assignment?> GetBySlotIdAndResourceIdAsync(Guid slotId, Guid resourceId, int? weekNum = null);

    Task<List<Assignment>> GetByResourceIdAsync(Guid resourceId);
    Task<List<Assignment>> GetBySchedulingPeriodIdAsync(Guid schedulingPeriodId);

    Task AddAsync(Assignment assignment);

    Task UpdateAsync(Assignment assignment);

    Task DeleteAsync(Assignment assignment);
    Task DeleteBySchedulingPeriodIdAsync(Guid schedulingPeriodId);
    Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);
    Task<int> DeleteAllByDepartmentIdAsync(Guid departmentId, CancellationToken ct = default);

    Task<bool> ExistsAsync(Guid id);
}
