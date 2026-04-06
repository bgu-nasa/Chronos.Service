using Chronos.Domain.Resources;

namespace Chronos.MainApi.Resources.Services;

public interface IActivityService
{
    Task<Activity> CreateActivityAsync(Guid organizationId, Guid subjectId, Guid assignedUserId, string activityType, int? expectedStudents, int duration);
    Task<Activity> GetActivityAsync(Guid organizationId, Guid activityId);
    Task<List<Activity>> GetActivitiesAsync(Guid organizationId);
    Task<List<Activity>> GetActivitiesBySubjectAsync(Guid organizationId, Guid subjectId);
    Task<List<Activity>> GetActivitiesBySchedulingPeriodAsync(Guid organizationId, Guid schedulingPeriodId);
    Task UpdateActivityAsync(Guid organizationId, Guid activityId, Guid subjectId, Guid assignedUserId, string activityType, int? expectedStudents, int duration);
    Task DeleteActivityAsync(Guid organizationId, Guid activityId);
}