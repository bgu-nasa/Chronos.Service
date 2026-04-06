using Chronos.Data.Repositories.Resources;
using Chronos.Domain.Resources;

namespace Chronos.MainApi.Resources.Services;

public class ActivityService(
    IActivityRepository activityRepository,
    ResourceValidationService validationService,
    ISubjectService subjectService,
    ILogger<ActivityService> logger) : IActivityService
{
    public async Task<Activity> CreateActivityAsync(Guid organizationId, Guid subjectId, Guid assignedUserId, string activityType,
        int? expectedStudents)
    {
        logger.LogInformation("Creating activity. OrganizationId: {OrganizationId}, SubjectId: {SubjectId}, AssignedUserId: {AssignedUserId}, ActivityType: {ActivityType}, ExpectedStudents: {ExpectedStudents}",
            organizationId, subjectId, assignedUserId, activityType, expectedStudents);

        await validationService.ValidationOrganizationAsync(organizationId);

        logger.LogInformation("Validating subject for activity creation. OrganizationId: {OrganizationId}, SubjectId: {SubjectId}", organizationId, subjectId);
        var subject = await subjectService.GetSubjectAsync(organizationId, subjectId);

        if (subject == null)
        {
            logger.LogWarning("Subject not found for activity creation. OrganizationId: {OrganizationId}, SubjectId: {SubjectId}", organizationId, subjectId);
            throw new Exception("Subject not found");
        }
        var activity = new Activity
        {
            OrganizationId = organizationId,
            SubjectId = subjectId,
            AssignedUserId = assignedUserId,
            ActivityType = activityType,
            ExpectedStudents = expectedStudents
        };

        await activityRepository.AddAsync(activity);

        logger.LogInformation("Activity created successfully. ActivityId: {ActivityId}, OrganizationId: {OrganizationId}", activity.Id, organizationId);
        return activity;
    }

    public async Task<Activity> GetActivityAsync(Guid organizationId, Guid activityId)
    {
        logger.LogInformation("Retrieving activity. OrganizationId: {OrganizationId}, ActivityId: {ActivityId}", organizationId, activityId);

        await validationService.ValidationOrganizationAsync(organizationId);
        var activity = await validationService.ValidateAndGetActivityAsync(organizationId, activityId);
        return activity;
    }

    public async Task<List<Activity>> GetActivitiesAsync(Guid organizationId)
    {
        logger.LogInformation("Retrieving all activities for organization. OrganizationId: {OrganizationId}", organizationId);
    
        await validationService.ValidationOrganizationAsync(organizationId);

        var allActivities = await activityRepository.GetAllAsync();
        var filteredActivities = allActivities
            .Where(a => a.OrganizationId == organizationId)
            .ToList();

        logger.LogInformation("Retrieved {Count} activities for organization. OrganizationId: {OrganizationId}", filteredActivities.Count, organizationId);
        return filteredActivities;
    }

    public async Task<List<Activity>> GetActivitiesBySubjectAsync(Guid organizationId, Guid subjectId)
    {
        logger.LogInformation("Retrieving activities for subject. OrganizationId: {OrganizationId}, SubjectId: {SubjectId}", organizationId, subjectId);

        await validationService.ValidationOrganizationAsync(organizationId);

        var allActivities =  await activityRepository.GetAllAsync();
        var filteredActivities = allActivities
            .Where(a => a.OrganizationId == organizationId && a.SubjectId == subjectId)
            .ToList();

        logger.LogInformation("Retrieved {Count} activities for subject. OrganizationId: {OrganizationId}, SubjectId: {SubjectId}", filteredActivities.Count, organizationId, subjectId);
        return filteredActivities;
    }

    public async Task<List<Activity>> GetActivitiesBySchedulingPeriodAsync(Guid organizationId, Guid schedulingPeriodId)
    {
        logger.LogInformation("Retrieving activities for scheduling period. OrganizationId: {OrganizationId}, SchedulingPeriodId: {SchedulingPeriodId}", organizationId, schedulingPeriodId);

        await validationService.ValidationOrganizationAsync(organizationId);

        var allActivities =  await activityRepository.GetAllAsync();
        var filteredActivities = allActivities
            .Where(a => a.OrganizationId == organizationId &&  subjectService.GetSubjectAsync(organizationId, a.SubjectId).Result.SchedulingPeriodId == schedulingPeriodId)
            .ToList();

        logger.LogInformation("Retrieved {Count} activities for scheduling period. OrganizationId: {OrganizationId}, SchedulingPeriodId: {SchedulingPeriodId}", filteredActivities.Count, organizationId, schedulingPeriodId);
        return filteredActivities;
    }

    public async Task UpdateActivityAsync(Guid organizationId, Guid activityId, Guid subjectId, Guid assignedUserId, string activityType,
        int? expectedStudents)
    {
        logger.LogInformation("Updating activity. OrganizationId: {OrganizationId}, ActivityId: {ActivityId}, SubjectId: {SubjectId}, AssignedUserId: {AssignedUserId}, ActivityType: {ActivityType}, ExpectedStudents: {ExpectedStudents}",
            organizationId, activityId, subjectId, assignedUserId, activityType, expectedStudents);

        await validationService.ValidationOrganizationAsync(organizationId);
        var activity = await validationService.ValidateAndGetActivityAsync(organizationId,  activityId);

        activity.SubjectId = subjectId;
        activity.AssignedUserId = assignedUserId;
        activity.ActivityType = activityType;
        activity.ExpectedStudents = expectedStudents;
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