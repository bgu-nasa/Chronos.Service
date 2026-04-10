using Chronos.MainApi.Schedule.Services;

namespace Chronos.MainApi.Resources.Services;

public class ExternalSchedulingPeriodService(
    ISchedulingPeriodService schedulingPeriodService,
    ILogger<ExternalSchedulingPeriodService> logger) : IExternalSchedulingPeriodService
{
    public async Task validateSchedulingPeriodAsync(Guid organizationId, Guid schedulingPeriodId)
    {
        logger.LogInformation("Validating scheduling period. OrganizationId: {OrganizationId}, SchedulingPeriodId: {SchedulingPeriodId}", organizationId, schedulingPeriodId);
        await schedulingPeriodService.validateSchedulingPeriodAsync(organizationId, schedulingPeriodId);
    }
}
