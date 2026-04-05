using Chronos.Data.Repositories.Resources;
using Chronos.Domain.Resources;

namespace Chronos.MainApi.Resources.Services;

public class ActivityService(
    IActivityRepository activityRepository,
    ResourceValidationService validationService,
    ILogger<ActivityService> logger) : IActivityService
{
    public async Task<Activity> CreateActivityAsync(Guid organizationId, Guid subjectId, Guid assignedUserId, string activityType,
        int? expectedStudents, int duration)
    {
        logger.LogInformation("Creating activity. OrganizationId: {OrganizationId}, SubjectId: {SubjectId}, AssignedUserId: {AssignedUserId}, ActivityType: {ActivityType}, ExpectedStudents: {ExpectedStudents}",
            organizationId, subjectId, assignedUserId, activityType, expectedStudents);
        
        if(duration <= 0)
        {
            logger.LogWarning("Invalid activity duration. Duration must be greater than zero. OrganizationId: {OrganizationId}, Duration: {Duration}", organizationId, duration);
            throw new ArgumentException("Duration must be greater than zero.", nameof(duration));
        }

        await validationService.ValidationOrganizationAsync(organizationId);
        
        var activity = new Activity
        {
            OrganizationId = organizationId,
            SubjectId = subjectId,
            AssignedUserId = assignedUserId,
            ActivityType = activityType,
            ExpectedStudents = expectedStudents,
            Duration = duration
        };

        await activityRepository.AddAsync(activity);

        logger.LogInformation("Activity created successfully. ActivityId: {ActivityId}, OrganizationId: {OrganizationId}", activity.Id, organizationId);
        return activity;
    }

    public async Task<Activity> GetActivityAsync(Guid organizationId, Guid activityId)
    {
        logger.LogDebug("Retrieving activity. OrganizationId: {OrganizationId}, ActivityId: {ActivityId}", organizationId, activityId);

        await validationService.ValidationOrganizationAsync(organizationId);
        var activity = await validationService.ValidateAndGetActivityAsync(organizationId, activityId);
        return activity;
    }

    public async Task<List<Activity>> GetActivitiesAsync(Guid organizationId)
    {
        logger.LogDebug("Retrieving all activities for organization. OrganizationId: {OrganizationId}", organizationId);

        await validationService.ValidationOrganizationAsync(organizationId);

        var allActivities = await activityRepository.GetAllAsync();
        var filteredActivities = allActivities
            .Where(a => a.OrganizationId == organizationId)
            .ToList();

        logger.LogDebug("Retrieved {Count} activities for organization. OrganizationId: {OrganizationId}", filteredActivities.Count, organizationId);
        return filteredActivities;
    }

    public async Task<List<Activity>> GetActivitiesBySubjectAsync(Guid organizationId, Guid subjectId)
    {
        logger.LogDebug("Retrieving activities for subject. OrganizationId: {OrganizationId}, SubjectId: {SubjectId}", organizationId, subjectId);

        await validationService.ValidationOrganizationAsync(organizationId);

        var allActivities =  await activityRepository.GetAllAsync();
        var filteredActivities = allActivities
            .Where(a => a.OrganizationId == organizationId && a.SubjectId == subjectId)
            .ToList();

        logger.LogDebug("Retrieved {Count} activities for subject. OrganizationId: {OrganizationId}, SubjectId: {SubjectId}", filteredActivities.Count, organizationId, subjectId);
        return filteredActivities;
    }

    public async Task UpdateActivityAsync(Guid organizationId, Guid activityId, Guid subjectId, Guid assignedUserId, string activityType,
        int? expectedStudents, int duration)
    {
        logger.LogInformation("Updating activity. OrganizationId: {OrganizationId}, ActivityId: {ActivityId}, SubjectId: {SubjectId}, AssignedUserId: {AssignedUserId}, ActivityType: {ActivityType}, ExpectedStudents: {ExpectedStudents}",
            organizationId, activityId, subjectId, assignedUserId, activityType, expectedStudents);

        if (duration <= 0)
        {
            logger.LogWarning("Invalid activity duration. Duration must be greater than zero. OrganizationId: {OrganizationId}, ActivityId: {ActivityId}, Duration: {Duration}", organizationId, activityId, duration);
            throw new ArgumentException("Duration must be greater than zero.", nameof(duration));
        }

        await validationService.ValidationOrganizationAsync(organizationId);
        var activity = await validationService.ValidateAndGetActivityAsync(organizationId,  activityId);

        activity.SubjectId = subjectId;
        activity.AssignedUserId = assignedUserId;
        activity.ActivityType = activityType;
        activity.ExpectedStudents = expectedStudents;
        activity.Duration = duration;
        await activityRepository.UpdateAsync(activity);

        logger.LogInformation("Activity updated successfully. ActivityId: {ActivityId}", activityId);
    }

    public async Task DeleteActivityAsync(Guid organizationId, Guid activityId)
    {
        logger.LogInformation("Deleting activity. OrganizationId: {OrganizationId}, ActivityId: {ActivityId}", organizationId, activityId);

        await validationService.ValidationOrganizationAsync(organizationId);
        var activity = await validationService.ValidateAndGetActivityAsync(organizationId, activityId);
        await activityRepository.DeleteAsync(activity);

        logger.LogInformation("Activity deleted successfully. OrganizationId: {OrganizationId}, ActivityId: {ActivityId}", organizationId, activityId);
    }
}