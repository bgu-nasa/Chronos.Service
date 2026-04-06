namespace Chronos.MainApi.Resources.Services;

public interface IExternalSchedulingPeriodService
{
    Task ValidateSchedulingPeriodAsync(Guid organizationId, Guid schedulingPeriodId);
}
