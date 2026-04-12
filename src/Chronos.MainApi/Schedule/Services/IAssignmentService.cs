using Chronos.Domain.Schedule;

namespace Chronos.MainApi.Schedule.Services;

public interface IAssignmentService
{
    Task<Guid> CreateAssignmentAsync(Guid organizationId, Guid slotId, Guid resourceId, Guid ScheduledItemId, int? weekNum = null);
    
    Task<Assignment> GetAssignmentAsync(Guid organizationId, Guid assignmentId);
    
    Task<List<Assignment>> GetAllAssignmentsAsync(
        Guid organizationId,
        Guid? slotId = null,
        Guid? resourceId = null,
        Guid? activityId = null,
        Guid? userId = null,
        Guid? schedulingPeriodId = null);
    
    Task<List<Assignment>> GetAssignmentsBySlotAsync(Guid organizationId, Guid slotId);
    
    Task<List<Assignment>> GetAssignmentsByActivityIdAsync(Guid organizationId, Guid activityId);
    
    Task<Assignment?> GetAssignmentBySlotAndResourceItemAsync(Guid organizationId, Guid slotId, Guid resourceId);
    
    Task UpdateAssignmentAsync(Guid organizationId, Guid assignmentId, Guid slotId, Guid resourceId , Guid activityId, int? weekNum = null);
    
    Task DeleteAssignmentAsync(Guid organizationId, Guid assignmentId);
    Task DeleteAssignmentsBySchedulingPeriodAsync(Guid organizationId, Guid schedulingPeriodId);
}