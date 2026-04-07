using Chronos.Domain.Schedule;

namespace Chronos.MainApi.Resources.Services;


public interface IExternalSchedulingPeriodService
{
    Task validateSchedulingPeriodAsync(Guid organizationId, Guid schedulingPeriodId);
}
