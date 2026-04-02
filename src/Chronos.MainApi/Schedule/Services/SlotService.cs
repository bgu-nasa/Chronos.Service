using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Schedule;
using Chronos.MainApi.Shared.ExternalMangement;
using Chronos.Shared.Exceptions;

namespace Chronos.MainApi.Schedule.Services;

public class SlotService(
    ISlotRepository slotRepository,
    IAssignmentRepository assignmentRepository,
    IManagementExternalService validationService,
    ISchedulingPeriodService schedulingPeriodService,
    ILogger<SlotService> logger) : ISlotService
{
    

    public async Task<Guid> CreateSlotAsync(Guid organizationId, Guid schedulingPeriodId, WeekDays weekday, TimeSpan fromTime, TimeSpan toTime)
    {
        logger.LogInformation(
            "Creating slot. OrganizationId: {OrganizationId}, SchedulingPeriodId: {SchedulingPeriodId}, Weekday: {Weekday}, FromTime: {FromTime}, ToTime: {ToTime}",
            organizationId, schedulingPeriodId, weekday, fromTime, toTime);
        await validationService.ValidateOrganizationAsync(organizationId);
        await ValidateSchedulingPeriodAsync(organizationId, schedulingPeriodId);
        await TimeRangeValidatorAsync(weekday, fromTime, toTime, schedulingPeriodId);
        var slot = new Slot
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            SchedulingPeriodId = schedulingPeriodId,
            Weekday = weekday.ToString(),
            FromTime = fromTime,
            ToTime = toTime
        };

        await slotRepository.AddAsync(slot);

        logger.LogInformation(
            "Slot created successfully. SlotId: {SlotId}, OrganizationId: {OrganizationId}",
            slot.Id, organizationId);

        return slot.Id;
    }
    public async Task<Slot> GetSlotAsync(Guid organizationId, Guid slotId)
    {
        logger.LogInformation(
            "Retrieving slot. OrganizationId: {OrganizationId}, SlotId: {SlotId}",
            organizationId, slotId);

        return await ValidateAndGetSlotAsync(organizationId, slotId);
    }
    
    public async Task<(List<Slot> Items, int TotalCount)> GetAllSlotsAsync(Guid organizationId, int page, int pageSize)
    {
        logger.LogInformation("Retrieving all slots OrganizationId: {OrganizationId}, Page: {Page}, PageSize: {PageSize}", organizationId, page, pageSize);

        await validationService.ValidateOrganizationAsync(organizationId);

        var (items, totalCount) = await slotRepository.GetAllAsync(page, pageSize);
        var filtered = items
            .Where(s => s.OrganizationId == organizationId)
            .OrderBy(s => s.Weekday)
            .ThenBy(s => s.FromTime)
            .ToList();

        logger.LogInformation("Retrieved {Count} slots for scheduling period. OrganizationId: {OrganizationId}", filtered.Count, organizationId);
        return (filtered, totalCount);
    }
    
    public async Task<(List<Slot> Items, int TotalCount)> GetSlotsBySchedulingPeriodAsync(Guid organizationId, Guid schedulingPeriodId, int page, int pageSize)
    {
        logger.LogInformation(
            "Retrieving slots by scheduling period. OrganizationId: {OrganizationId}, SchedulingPeriodId: {SchedulingPeriodId}, Page: {Page}, PageSize: {PageSize}",
            organizationId, schedulingPeriodId, page, pageSize);

        await validationService.ValidateOrganizationAsync(organizationId);

        var (items, totalCount) = await slotRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId, page, pageSize);
        var filtered = items
            .Where(s => s.OrganizationId == organizationId)
            .OrderBy(s => s.Weekday)
            .ThenBy(s => s.FromTime)
            .ToList();

        logger.LogInformation("Retrieved {Count} slots for scheduling period. SchedulingPeriodId: {SchedulingPeriodId}, OrganizationId: {OrganizationId}", filtered.Count, schedulingPeriodId, organizationId);
        return (filtered, totalCount);
    }
    
    public async Task UpdateSlotAsync(Guid organizationId, Guid slotId, WeekDays weekday, TimeSpan fromTime, TimeSpan toTime)
    {
        logger.LogInformation(
            "Updating slot. OrganizationId: {OrganizationId}, SlotId: {SlotId}",
            organizationId, slotId);

        var slot = await ValidateAndGetSlotAsync(organizationId, slotId);
        await TimeRangeValidatorAsync(weekday, fromTime, toTime, slot.SchedulingPeriodId , slotId);
        slot.Weekday = weekday.ToString();
        slot.FromTime = fromTime;
        slot.ToTime = toTime;

        await slotRepository.UpdateAsync(slot);

        logger.LogInformation(
            "Slot updated successfully. SlotId: {SlotId}, OrganizationId: {OrganizationId}",
            slot.Id, organizationId);
    }
    
    public async Task DeleteSlotAsync(Guid organizationId, Guid slotId)
    {
        logger.LogInformation(
            "Deleting slot. OrganizationId: {OrganizationId}, SlotId: {SlotId}",
            organizationId, slotId);

        var slot = await ValidateAndGetSlotAsync(organizationId, slotId);
        var (assignments, _) = await assignmentRepository.GetBySlotIdAsync(slotId, 1, int.MaxValue);
        foreach(var assignment in assignments)
        {
            await assignmentRepository.DeleteAsync(assignment);
        }
        await slotRepository.DeleteAsync(slot);

        logger.LogInformation(
            "Slot deleted successfully. SlotId: {SlotId}, OrganizationId: {OrganizationId}",
            slot.Id, organizationId);
    }

    private async Task TimeRangeValidatorAsync(WeekDays weekday, TimeSpan fromTime, TimeSpan toTime, Guid schedulingPeriodId ,Guid? slotId = null)
    {
        if (fromTime >= toTime)
        {
            logger.LogInformation(
                "Invalid time range. FromTime: {FromTime}, ToTime: {ToTime}",
                fromTime, toTime);
            throw new BadRequestException("FromTime must be earlier than ToTime");
        }
        if (fromTime < TimeSpan.Zero || toTime < TimeSpan.Zero)
        {
            logger.LogInformation(
                "Invalid time range: negative time. FromTime: {FromTime}, ToTime: {ToTime}",
                fromTime, toTime);
            throw new BadRequestException("FromTime and ToTime must be non-negative");
        }

        var (slots, _) = await slotRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId, 1, int.MaxValue);
        foreach (var slot in slots)
        {
            if(slotId != null && slot.Id == slotId)
            {
                continue;
            }
            if(slot.Weekday.Equals(weekday.ToString()))
            {
                if((fromTime < slot.ToTime) && (toTime > slot.FromTime))
                {
                    logger.LogInformation(
                        "Time range overlaps with existing slot. weekday: {Weekday}, FromTime: {FromTime}, ToTime: {ToTime}",
                        weekday, fromTime, toTime);
                    throw new BadRequestException("The specified time range overlaps with an existing slot.");
                }
            }
        }
        
    }

    private async Task<Slot> ValidateAndGetSlotAsync(Guid organizationId, Guid slotId)
    {
        var slot = await slotRepository.GetByIdAsync(slotId);
        if (slot == null || slot.OrganizationId != organizationId)
        {
            logger.LogInformation(
                "Slot not found or does not belong to the organization. SlotId: {SlotId}, OrganizationId: {OrganizationId}",
                slotId, organizationId);
            throw new NotFoundException($"Slot with ID '{slotId}' not found in organization '{organizationId}'.");
        }

        return slot;
    }
        private async Task ValidateSchedulingPeriodAsync(Guid organizationId, Guid schedulingPeriodId)
    {
        var period = await schedulingPeriodService.GetSchedulingPeriodAsync(organizationId, schedulingPeriodId);
        if (period == null)
        {
            logger.LogInformation("Scheduling period not found for Organization {OrganizationId} with SchedulingPeriodId {SchedulingPeriodId}", organizationId, schedulingPeriodId);
            throw new NotFoundException($"Scheduling period with ID '{schedulingPeriodId}' not found in organization '{organizationId}'.");
        }
        if(period.ToDate < DateTime.Now)
        {
            logger.LogInformation(
                "Cannot operate on a scheduling period that has already ended. SchedulingPeriodId: {SchedulingPeriodId}",
                schedulingPeriodId);
            throw new BadRequestException("Cannot operate on a scheduling period that has already ended.");
        }
    }
}