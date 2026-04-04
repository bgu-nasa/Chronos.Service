using Chronos.Domain.Resources;

namespace Chronos.MainApi.Resources.Services;

public interface IActivityService
{
    Task<Activity> CreateActivityAsync(Guid organizationId, Guid subjectId, Guid assignedUserId, string activityType, int? expectedStudents);
    Task<Activity> GetActivityAsync(Guid organizationId, Guid activityId);
    Task<List<Activity>> GetActivitiesAsync(Guid organizationId);
    Task<List<Activity>> GetActivitiesBySubjectAsync(Guid organizationId, Guid subjectId);
    Task UpdateActivityAsync(Guid organizationId, Guid activityId, Guid subjectId, Guid assignedUserId, string activityType, int? expectedStudents);
    Task DeleteActivityAsync(Guid organizationId, Guid activityId);
    Task DeleteActivitiesBySubjectIdAsync(Guid organizationId, Guid subjectId);
    Task DeleteActivitiesByAssignedUserIdAsync(Guid organizationId, Guid assignedUserId);
}