using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Schedule;
using Chronos.MainApi.Shared.ExternalMangement;
using Chronos.Shared.Exceptions;

namespace Chronos.MainApi.Schedule.Services;

public class AssignmentService(
    IAssignmentRepository assignmentRepository,
    IManagementExternalService validationService,
    IExternalActivityService activityService,
    IExternalSubjectService subjectService,
    ISlotService slotService,
    IExternalResourceService resourceService,
    ISchedulingPeriodService schedulingPeriodService,
    ILogger<AssignmentService> logger) : IAssignmentService
{
    public async Task<Guid> CreateAssignmentAsync(Guid organizationId, Guid slotId, Guid resourceId, Guid activityId)
    {
        logger.LogInformation(
            "Creating assignment. OrganizationId: {OrganizationId}, SlotId: {SlotId}, ResourceId: {ResourceId}, ActivityId: {ActivityId}",
            organizationId, slotId, resourceId, activityId);
        
        await validationService.ValidateOrganizationAsync(organizationId);
        await ValidateDataAsync(organizationId, slotId, resourceId, activityId);
        await validateTwoAssignmentsPerSlotPerResourceAsync(organizationId, slotId, resourceId);
        var assignment = new Assignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            SlotId = slotId,
            ResourceId = resourceId,
            ActivityId = activityId,
        };
        
        await assignmentRepository.AddAsync(assignment);
        
        logger.LogInformation(
            "Assignment created successfully. AssignmentId: {AssignmentId}, OrganizationId: {OrganizationId}",
            assignment.Id, organizationId);
        
        return assignment.Id;
    }

    public async Task<Assignment> GetAssignmentAsync(Guid organizationId, Guid assignmentId)
    {
        logger.LogInformation(
            "Retrieving assignment. OrganizationId: {OrganizationId}, AssignmentId: {AssignmentId}",
            organizationId, assignmentId);
        
        var assignment = await ValidateAndGetAssignmentAsync(organizationId, assignmentId);
        
        logger.LogInformation(
            "Assignment retrieved successfully. AssignmentId: {AssignmentId}, OrganizationId: {OrganizationId}",
            assignment.Id, organizationId);
        
        return assignment;
    }
    
    public async Task<List<Assignment>> GetAllAssignmentsAsync(
        Guid organizationId,
        Guid? slotId = null,
        Guid? resourceId = null,
        Guid? activityId = null,
        Guid? userId = null,
        Guid? schedulingPeriodId = null)
    {
        logger.LogInformation("Retrieving all assignments for organization. OrganizationId: {OrganizationId}", organizationId);

        await validationService.ValidateOrganizationAsync(organizationId);

        var query = new AssignmentQuery
        {
            OrganizationId = organizationId,
            SlotId = slotId,
            ResourceId = resourceId,
            ActivityId = activityId,
            UserId = userId,
            SchedulingPeriodId = schedulingPeriodId,
        };

        var assignments = await assignmentRepository.GetAllAsync(query);

        logger.LogInformation("Retrieved {Count} assignments for organization. OrganizationId: {OrganizationId}", assignments.Count, organizationId);
        return assignments;
    }
    
    public async Task<List<Assignment>> GetAssignmentsBySlotAsync(Guid organizationId, Guid slotId)
    {
        logger.LogInformation(
            "Retrieving assignments by slot. OrganizationId: {OrganizationId}, SlotId: {SlotId}",
            organizationId, slotId);
        await validationService.ValidateOrganizationAsync(organizationId);
        
        var all = await assignmentRepository.GetBySlotIdAsync(slotId);
        var filtered = all
            .Where(a => a.OrganizationId == organizationId)
            .ToList();
        
        logger.LogInformation("Retrieved {Count} assignments for slot. OrganizationId: {OrganizationId}, SlotId: {SlotId}", filtered.Count, organizationId, slotId);
        return filtered;
    }
    
    public async Task<List<Assignment>> GetAssignmentsByActivityIdAsync(Guid organizationId, Guid activityId)
    {
        logger.LogInformation(
            "Retrieving assignments by activity id. OrganizationId: {OrganizationId}, activityId: {activityId}",
            organizationId, activityId);
        await validationService.ValidateOrganizationAsync(organizationId);
        
        var all = await assignmentRepository.GetByActivityIdAsync(activityId);
        var filtered = all
            .Where(a => a.OrganizationId == organizationId)
            .ToList();
        
        logger.LogInformation("Retrieved {Count} assignments for activity. OrganizationId: {OrganizationId}, activityId: {activityId}", filtered.Count, organizationId, activityId);
        return filtered;
    }
    
    public async Task<Assignment?> GetAssignmentBySlotAndResourceItemAsync(Guid organizationId, Guid slotId, Guid resourceId)
    {
        logger.LogInformation(
            "Retrieving assignment by slot and resource. OrganizationId: {OrganizationId}, SlotId: {SlotId}, ResourceId: {ResourceId}",
            organizationId, slotId, resourceId);
        await validationService.ValidateOrganizationAsync(organizationId);
        
        var assignment = await assignmentRepository.GetBySlotIdAndResourceIdAsync(slotId, resourceId);
        if (assignment == null || assignment.OrganizationId != organizationId)
        {
            logger.LogInformation("Assignment not found for Organization {OrganizationId} with SlotId {SlotId} and ResourceId {ResourceId}", organizationId, slotId, resourceId);
            return null;
        }
        
        logger.LogInformation(
            "Assignment retrieved successfully. AssignmentId: {AssignmentId}, OrganizationId: {OrganizationId}",
            assignment.Id, organizationId);
        
        return assignment;
    }
    
    public async Task UpdateAssignmentAsync(Guid organizationId, Guid assignmentId, Guid slotId, Guid resourceId, Guid activityId)
    {
        logger.LogInformation(
            "Updating assignment. OrganizationId: {OrganizationId}, AssignmentId: {AssignmentId}",
            organizationId, assignmentId);
        
        var assignment = await ValidateAndGetAssignmentAsync(organizationId, assignmentId);
        await ValidateDataAsync(organizationId, slotId, resourceId, activityId);
        await validateTwoAssignmentsPerSlotPerResourceAsync(organizationId, slotId, resourceId , assignmentId);
        assignment.SlotId = slotId;
        assignment.ResourceId = resourceId;
        assignment.ActivityId = activityId;
        
        await assignmentRepository.UpdateAsync(assignment);
        
        logger.LogInformation(
            "Assignment updated successfully. AssignmentId: {AssignmentId}, OrganizationId: {OrganizationId}",
            assignment.Id, organizationId);
    }
    
    
    public async Task DeleteAssignmentAsync(Guid organizationId, Guid assignmentId)
    {
        logger.LogInformation(
            "Deleting assignment. OrganizationId: {OrganizationId}, AssignmentId: {AssignmentId}",
            organizationId, assignmentId);
        
        var assignment = await ValidateAndGetAssignmentAsync(organizationId, assignmentId);
        await assignmentRepository.DeleteAsync(assignment);
        
        logger.LogInformation(
            "Assignment deleted successfully. AssignmentId: {AssignmentId}, OrganizationId: {OrganizationId}",
            assignment.Id, organizationId);
    }

    public async Task DeleteAssignmentsBySchedulingPeriodAsync(Guid organizationId, Guid schedulingPeriodId)
    {
        logger.LogInformation(
            "Deleting all assignments for scheduling period. OrganizationId: {OrganizationId}, SchedulingPeriodId: {SchedulingPeriodId}",
            organizationId, schedulingPeriodId);

        await validationService.ValidateOrganizationAsync(organizationId);
        var period = await schedulingPeriodService.GetSchedulingPeriodAsync(organizationId, schedulingPeriodId);
        if (period == null)
        {
            throw new NotFoundException($"Scheduling period with ID {schedulingPeriodId} not found in organization {organizationId}.");
        }

        await assignmentRepository.DeleteBySchedulingPeriodIdAsync(schedulingPeriodId);

        logger.LogInformation(
            "All assignments deleted for scheduling period. OrganizationId: {OrganizationId}, SchedulingPeriodId: {SchedulingPeriodId}",
            organizationId, schedulingPeriodId);
    }
    
    private async Task<Assignment> ValidateAndGetAssignmentAsync(Guid organizationId, Guid assignmentId)
    {
        var assignment = await assignmentRepository.GetByIdAsync(assignmentId);
        if (assignment == null || assignment.OrganizationId != organizationId)
        {
            logger.LogInformation("Assignment {AssignmentId} not found for Organization {OrganizationId}", assignmentId, organizationId);
            throw new NotFoundException($"Assignment with ID {assignmentId} not found in organization {organizationId}.");
        }

        return assignment;
    }

    private async Task ValidateDataAsync(Guid organizationId, Guid slotId, Guid resourceId, Guid activityId)
    {
        var resourceExists = await resourceService.GetResourceAsync(organizationId, resourceId);
        if(resourceExists == null)
        {
            logger.LogInformation("Resource {ResourceId} not found for Organization {OrganizationId}", resourceId, organizationId);
            throw new NotFoundException($"Resource with ID {resourceId} not found in organization {organizationId}.");
        }
        var activity = await activityService.GetActivityAsync(organizationId, activityId);
        if(activity == null)
        {
            logger.LogInformation("Activity {ActivityId} not found for Organization {OrganizationId}", activityId, organizationId);
            throw new NotFoundException($"Activity with ID {activityId} not found in organization {organizationId}.");
        }
        var subject = await subjectService.GetSubjectAsync(organizationId, activity.SubjectId);
        if(subject == null)
        {
            logger.LogInformation("Subject {SubjectId} not found for Organization {OrganizationId}", activity.SubjectId, organizationId);
            throw new NotFoundException($"Subject with ID {activity.SubjectId} not found in organization {organizationId}.");
        }
        var schedulingPeriod = await schedulingPeriodService.GetSchedulingPeriodAsync(subject.OrganizationId, subject.SchedulingPeriodId);
        if(schedulingPeriod == null)
        {
            logger.LogInformation("Scheduling period {SchedulingPeriodId} not found for Organization {OrganizationId}", subject.SchedulingPeriodId, organizationId);
            throw new NotFoundException($"Scheduling period with ID {subject.SchedulingPeriodId} not found in organization {organizationId}.");
        }
        var slot = await slotService.GetSlotAsync(organizationId, slotId);
        if(slot == null)
        {
            logger.LogInformation("Slot {SlotId} not found for Organization {OrganizationId}", slotId, organizationId);
            throw new NotFoundException($"Slot with ID {slotId} not found in organization {organizationId}.");
        }

        if(schedulingPeriod.Id != slot.SchedulingPeriodId)
        {
            logger.LogInformation("Slot {SlotId} does not belong to the same scheduling period as the activity {ActivityId}", slotId, activityId);
            throw new BadRequestException("Slot does not belong to the same scheduling period as the activity");
        }
        if(activity.ExpectedStudents != null && resourceExists.Capacity < activity.ExpectedStudents)
        {
            logger.LogInformation("Resource {ResourceId} capacity is less than expected students for Activity {ActivityId}", resourceId, activityId);
            throw new BadRequestException("Resource capacity is less than expected students for the activity");
        }

    }
    private async Task validateTwoAssignmentsPerSlotPerResourceAsync(Guid organizationId, Guid slotId, Guid resourceId, Guid? excludeAssignmentId = null) 
    {
        var slot = await slotService.GetSlotAsync(organizationId, slotId);
        if(slot == null)
        {
            logger.LogInformation("Slot {SlotId} not found for Organization {OrganizationId}", slotId, organizationId);
            throw new NotFoundException($"Slot with ID {slotId} not found in organization {organizationId}.");
        }
        var existingAssignment = await assignmentRepository.GetByResourceIdAsync(resourceId);
        foreach (var assignment in existingAssignment)
        {
            if(excludeAssignmentId != null && assignment.Id == excludeAssignmentId)
            {
                continue;
            }
            var assignedSlot = await slotService.GetSlotAsync(organizationId, assignment.SlotId);
            if(assignedSlot.Weekday == slot.Weekday)
            {
                if((slot.FromTime < assignedSlot.ToTime) && (assignedSlot.FromTime < slot.ToTime))
                {
                    logger.LogInformation("Resource {ResourceId} already has an assignment in Slot {SlotId} which overlaps with Slot {NewSlotId}", resourceId, assignedSlot.Id, slotId);
                    throw new BadRequestException("Resource already has an assignment in a slot that overlaps with the new slot");
                }
            }
            
        }
    }
    
}