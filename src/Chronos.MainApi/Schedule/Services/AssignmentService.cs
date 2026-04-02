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
    
    public async Task<(List<Assignment> Items, int TotalCount)> GetAllAssignmentsAsync(Guid organizationId, int page, int pageSize)
    {
        logger.LogInformation("Retrieving all assignments for organization. OrganizationId: {OrganizationId}, Page: {Page}, PageSize: {PageSize}", organizationId, page, pageSize);
        
        await validationService.ValidateOrganizationAsync(organizationId);
        var (items, totalCount) = await assignmentRepository.GetAllAsync(page, pageSize);
        var filtered = items
            .Where(a => a.OrganizationId == organizationId)
            .ToList();
        
        logger.LogInformation("Retrieved {Count} assignments for organization. OrganizationId: {OrganizationId}", filtered.Count, organizationId);
        return (filtered, totalCount);
    }
    
    public async Task<(List<Assignment> Items, int TotalCount)> GetAssignmentsBySlotAsync(Guid organizationId, Guid slotId, int page, int pageSize)
    {
        logger.LogInformation(
            "Retrieving assignments by slot. OrganizationId: {OrganizationId}, SlotId: {SlotId}, Page: {Page}, PageSize: {PageSize}",
            organizationId, slotId, page, pageSize);
        await validationService.ValidateOrganizationAsync(organizationId);
        
        var (items, totalCount) = await assignmentRepository.GetBySlotIdAsync(slotId, page, pageSize);
        var filtered = items
            .Where(a => a.OrganizationId == organizationId)
            .ToList();
        
        logger.LogInformation("Retrieved {Count} assignments for slot. OrganizationId: {OrganizationId}, SlotId: {SlotId}", filtered.Count, organizationId, slotId);
        return (filtered, totalCount);
    }
    
    public async Task<(List<Assignment> Items, int TotalCount)> GetAssignmentsByActivityIdAsync(Guid organizationId, Guid activityId, int page, int pageSize)
    {
        logger.LogInformation(
            "Retrieving assignments by activity id. OrganizationId: {OrganizationId}, activityId: {activityId}, Page: {Page}, PageSize: {PageSize}",
            organizationId, activityId, page, pageSize);
        await validationService.ValidateOrganizationAsync(organizationId);
        
        var (items, totalCount) = await assignmentRepository.GetByActivityIdAsync(activityId, page, pageSize);
        var filtered = items
            .Where(a => a.OrganizationId == organizationId)
            .ToList();
        
        logger.LogInformation("Retrieved {Count} assignments for activity. OrganizationId: {OrganizationId}, activityId: {activityId}", filtered.Count, organizationId, activityId);
        return (filtered, totalCount);
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