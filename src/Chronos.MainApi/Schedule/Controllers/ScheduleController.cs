using Chronos.MainApi.Schedule.Contracts;
using Chronos.MainApi.Schedule.Extensions;
using Chronos.MainApi.Schedule.Services;
using Chronos.MainApi.Shared.Contracts;
using Chronos.MainApi.Shared.Controllers.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chronos.MainApi.Schedule.Controllers;

[ApiController]
[Route("api/schedule/scheduling")]
public class ScheduleController(
    ILogger<ScheduleController> logger,
    ISchedulingPeriodService schedulingPeriodService,
    ISlotService slotService,
    IAssignmentService assignmentService)
    : ControllerBase
{
    private const string ViewerPolicy = "OrgRole:Viewer";
    private const string ResourceManagerPolicy = "OrgRole:ResourceManager";


    [Authorize(Policy = ResourceManagerPolicy)]
    [HttpPost("periods")]
    public async Task<IActionResult> CreateSchedulingPeriod([FromBody] CreateSchedulingPeriodRequest request)
    {
        logger.LogInformation("Create scheduling period endpoint was called");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var periodId = await schedulingPeriodService.CreateSchedulingPeriodAsync(
            organizationId,
            request.Name,
            request.FromDate,
            request.ToDate);

        var period = await schedulingPeriodService.GetSchedulingPeriodAsync(organizationId, periodId);
        var response = period.ToSchedulingPeriodResponse();

        return CreatedAtAction(nameof(GetSchedulingPeriod), new { schedulingPeriodId = periodId }, response);
    }

    [Authorize (Policy = ViewerPolicy)]
    [HttpGet("periods/{schedulingPeriodId}")]
    public async Task<IActionResult> GetSchedulingPeriod(Guid schedulingPeriodId)
    {
        logger.LogInformation("Get scheduling period endpoint was called for {PeriodId}", schedulingPeriodId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var period = await schedulingPeriodService.GetSchedulingPeriodAsync(organizationId, schedulingPeriodId);
        if (period == null)
            return NotFound();

        return Ok(period.ToSchedulingPeriodResponse());
    }

    [Authorize (Policy = ViewerPolicy)]
    [HttpGet("periods")]
    public async Task<IActionResult> GetAllSchedulingPeriods([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        logger.LogInformation("Get all scheduling periods endpoint was called");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var (periods, totalCount) = await schedulingPeriodService.GetAllSchedulingPeriodsAsync(organizationId, page, pageSize);
        var responses = periods.Select(p => p.ToSchedulingPeriodResponse()).ToList();

        return Ok(new PagedResponse<SchedulingPeriodResponse>
        {
            Items = responses,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    [Authorize (Policy = ResourceManagerPolicy)]
    [HttpPatch("periods/{schedulingPeriodId}")]
    public async Task<IActionResult> UpdateSchedulingPeriod(
        Guid schedulingPeriodId,
        [FromBody] UpdateSchedulingPeriodRequest request)
    {
        logger.LogInformation("Update scheduling period endpoint was called for {PeriodId}", schedulingPeriodId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        await schedulingPeriodService.UpdateSchedulingPeriodAsync(
            organizationId,
            schedulingPeriodId,
            request.Name,
            request.FromDate,
            request.ToDate);

        return NoContent();
    }

    [Authorize (Policy = ResourceManagerPolicy)]
    [HttpDelete("periods/{schedulingPeriodId}")]
    public async Task<IActionResult> DeleteSchedulingPeriod(Guid schedulingPeriodId)
    {
        logger.LogInformation("Delete scheduling period endpoint was called for {PeriodId}", schedulingPeriodId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        await schedulingPeriodService.DeleteSchedulingPeriodAsync(organizationId, schedulingPeriodId);

        return NoContent();
    }


    [Authorize (Policy = ResourceManagerPolicy)]
    [HttpPost("slots")]
    public async Task<IActionResult> CreateSlot([FromBody] CreateSlotRequest request)
    {
        logger.LogInformation("Create slot endpoint was called");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var slotId = await slotService.CreateSlotAsync(
            organizationId,
            request.SchedulingPeriodId,
            request.Weekday,
            request.FromTime,
            request.ToTime);

        var slot = await slotService.GetSlotAsync(organizationId, slotId);
        var response = slot.ToSlotResponse();

        return CreatedAtAction(nameof(GetSlot), new { slotId }, response);
    }

    [Authorize (Policy = ViewerPolicy)]
    [HttpGet("slots/{slotId}")]
    public async Task<IActionResult> GetSlot(Guid slotId)
    {
        logger.LogInformation("Get slot endpoint was called for {SlotId}", slotId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var slot = await slotService.GetSlotAsync(organizationId, slotId);
        if (slot == null)
            return NotFound();

        return Ok(slot.ToSlotResponse());
    }

    [Authorize (Policy = ViewerPolicy)]
    [HttpGet("slots")]
    public async Task<IActionResult> GetAllSlots([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        logger.LogInformation("Get all slots endpoint was called");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var (slots, totalCount) = await slotService.GetAllSlotsAsync(organizationId, page, pageSize);
        var responses = slots.Select(s => s.ToSlotResponse()).ToList();

        return Ok(new PagedResponse<SlotResponse>
        {
            Items = responses,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    [Authorize (Policy = ViewerPolicy)]
    [HttpGet("periods/{schedulingPeriodId}/slots")]
    public async Task<IActionResult> GetSlotsBySchedulingPeriod(Guid schedulingPeriodId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        logger.LogInformation("Get slots by scheduling period endpoint was called for {PeriodId}", schedulingPeriodId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var (slots, totalCount) = await slotService.GetSlotsBySchedulingPeriodAsync(organizationId, schedulingPeriodId, page, pageSize);
        var responses = slots.Select(s => s.ToSlotResponse()).ToList();

        return Ok(new PagedResponse<SlotResponse>
        {
            Items = responses,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    [Authorize (Policy = ResourceManagerPolicy)]
    [HttpPatch("slots/{slotId}")]
    public async Task<IActionResult> UpdateSlot(Guid slotId, [FromBody] UpdateSlotRequest request)
    {
        logger.LogInformation("Update slot endpoint was called for {SlotId}", slotId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        await slotService.UpdateSlotAsync(
            organizationId,
            slotId,
            request.Weekday,
            request.FromTime,
            request.ToTime);

        return NoContent();
    }

    [Authorize (Policy = ResourceManagerPolicy)]
    [HttpDelete("slots/{slotId}")]
    public async Task<IActionResult> DeleteSlot(Guid slotId)
    {
        logger.LogInformation("Delete slot endpoint was called for {SlotId}", slotId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        await slotService.DeleteSlotAsync(organizationId, slotId);

        return NoContent();
    }


    [Authorize (Policy = ResourceManagerPolicy)]
    [HttpPost("assignments")]
    public async Task<IActionResult> CreateAssignment([FromBody] CreateAssignmentRequest request)
    {
        logger.LogInformation("Create assignment endpoint was called");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var assignmentId = await assignmentService.CreateAssignmentAsync(
            organizationId,
            request.SlotId,
            request.ResourceId,
            request.ActivityId);

        var assignment = await assignmentService.GetAssignmentAsync(organizationId, assignmentId);
        var response = assignment.ToAssignmentResponse();

        return CreatedAtAction(nameof(GetAssignment), new { assignmentId }, response);
    }

    [Authorize (Policy = ViewerPolicy)]
    [HttpGet("assignments/{assignmentId}")]
    public async Task<IActionResult> GetAssignment(Guid assignmentId)
    {
        logger.LogInformation("Get assignment endpoint was called for {AssignmentId}", assignmentId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var assignment = await assignmentService.GetAssignmentAsync(organizationId, assignmentId);
        if (assignment == null)
            return NotFound();

        return Ok(assignment.ToAssignmentResponse());
    }

    [Authorize (Policy = ViewerPolicy)]
    [HttpGet("assignments")]
    public async Task<IActionResult> GetAllAssignments([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        logger.LogInformation("Get all assignments endpoint was called");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var (assignments, totalCount) = await assignmentService.GetAllAssignmentsAsync(organizationId, page, pageSize);
        var responses = assignments.Select(a => a.ToAssignmentResponse()).ToList();

        return Ok(new PagedResponse<AssignmentResponse>
        {
            Items = responses,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    [Authorize (Policy = ViewerPolicy)]
    [HttpGet("slots/{slotId}/assignments")]
    public async Task<IActionResult> GetAssignmentsBySlot(Guid slotId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        logger.LogInformation("Get assignments by slot endpoint was called for {SlotId}", slotId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var (assignments, totalCount) = await assignmentService.GetAssignmentsBySlotAsync(organizationId, slotId, page, pageSize);
        var responses = assignments.Select(a => a.ToAssignmentResponse()).ToList();

        return Ok(new PagedResponse<AssignmentResponse>
        {
            Items = responses,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    [Authorize (Policy = ViewerPolicy)]
    [HttpGet("activities/{activityId}/assignments")]
    public async Task<IActionResult> GetAssignmentsByActivity(Guid activityId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        logger.LogInformation("Get assignments by activity endpoint was called for {ActivityId}", activityId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var (assignments, totalCount) = await assignmentService.GetAssignmentsByActivityIdAsync(organizationId, activityId, page, pageSize);
        var responses = assignments.Select(a => a.ToAssignmentResponse()).ToList();

        return Ok(new PagedResponse<AssignmentResponse>
        {
            Items = responses,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    [Authorize (Policy = ViewerPolicy)]
    [HttpGet("slots/{slotId}/resources/{resourceId}/assignment")]
    public async Task<IActionResult> GetAssignmentBySlotAndResource(Guid slotId, Guid resourceId)
    {
        logger.LogInformation("Get assignment by slot and resource endpoint was called for {SlotId} and {ResourceId}", slotId, resourceId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var assignment = await assignmentService.GetAssignmentBySlotAndResourceItemAsync(organizationId, slotId, resourceId);
        if (assignment == null)
            return NotFound();

        return Ok(assignment.ToAssignmentResponse());
    }

    [Authorize (Policy = ResourceManagerPolicy)]
    [HttpPatch("assignments/{assignmentId}")]
    public async Task<IActionResult> UpdateAssignment(Guid assignmentId, [FromBody] UpdateAssignmentRequest request)
    {
        logger.LogInformation("Update assignment endpoint was called for {AssignmentId}", assignmentId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        await assignmentService.UpdateAssignmentAsync(
            organizationId,
            assignmentId,
            request.SlotId,
            request.ResourceId,
            request.ActivityId);

        return NoContent();
    }

    [Authorize (Policy = ResourceManagerPolicy)]
    [HttpDelete("assignments/{assignmentId}")]
    public async Task<IActionResult> DeleteAssignment(Guid assignmentId)
    {
        logger.LogInformation("Delete assignment endpoint was called for {AssignmentId}", assignmentId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        await assignmentService.DeleteAssignmentAsync(organizationId, assignmentId);

        return NoContent();
    }
}
