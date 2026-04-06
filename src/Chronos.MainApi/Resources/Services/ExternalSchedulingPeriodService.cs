using Chronos.Data.Repositories.Schedule;
using Chronos.MainApi.Schedule.Services;
using Chronos.Shared.Exceptions;

namespace Chronos.MainApi.Resources.Services;

public class ExternalSchedulingPeriodService(
    ISchedulingPeriodService schedulingPeriodService,
    ILogger<ExternalSchedulingPeriodService> logger) : IExternalSchedulingPeriodService
{
    public async Task ValidateSchedulingPeriodAsync(Guid organizationId, Guid schedulingPeriodId)
    {
        logger.LogInformation("Validating scheduling period. OrganizationId: {OrganizationId}, SchedulingPeriodId: {SchedulingPeriodId}", organizationId, schedulingPeriodId);
        var schedulingPeriod = await schedulingPeriodService.GetSchedulingPeriodAsync(organizationId, schedulingPeriodId);
        if (schedulingPeriod == null || schedulingPeriod.OrganizationId != organizationId)
        {
            logger.LogWarning("Scheduling period not found or does not belong to organization. SchedulingPeriodId: {SchedulingPeriodId}, OrganizationId: {OrganizationId}", schedulingPeriodId, organizationId);
            throw new NotFoundException("Scheduling period not found");
        }
        if(schedulingPeriod.ToDate < DateTime.UtcNow)
        {
            logger.LogWarning("Scheduling period is in the past. SchedulingPeriodId: {SchedulingPeriodId}, OrganizationId: {OrganizationId}", schedulingPeriodId, organizationId);
            throw new BadRequestException("Scheduling period is in the past");
        }
    }
}
