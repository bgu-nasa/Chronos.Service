using Chronos.Domain.Schedule;

namespace Chronos.MainApi.Schedule.Services;

public interface IAssignmentService
{
    Task<Guid> CreateAssignmentAsync(Guid organizationId, Guid slotId, Guid resourceId, Guid ScheduledItemId);
    
    Task<Assignment> GetAssignmentAsync(Guid organizationId, Guid assignmentId);
    
    Task<(List<Assignment> Items, int TotalCount)> GetAllAssignmentsAsync(Guid organizationId, int page, int pageSize);
    
    Task<(List<Assignment> Items, int TotalCount)> GetAssignmentsBySlotAsync(Guid organizationId, Guid slotId, int page, int pageSize);
    
    Task<(List<Assignment> Items, int TotalCount)> GetAssignmentsByActivityIdAsync(Guid organizationId, Guid activityId, int page, int pageSize);
    
    Task<Assignment?> GetAssignmentBySlotAndResourceItemAsync(Guid organizationId, Guid slotId, Guid resourceId);
    
    Task UpdateAssignmentAsync(Guid organizationId, Guid assignmentId, Guid slotId, Guid resourceId , Guid activityId);
    
    Task DeleteAssignmentAsync(Guid organizationId, Guid assignmentId);
    
    
}