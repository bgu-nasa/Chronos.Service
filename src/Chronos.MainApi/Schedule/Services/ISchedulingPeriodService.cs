using Chronos.Domain.Schedule;

namespace Chronos.MainApi.Schedule.Services;

public interface ISchedulingPeriodService
{
    Task<Guid> CreateSchedulingPeriodAsync(Guid organizationId, string name, DateTime fromDate, DateTime toDate);

    Task<SchedulingPeriod> GetSchedulingPeriodAsync(Guid organizationId, Guid schedulingPeriodId);
    
    Task<SchedulingPeriod> GetSchedulingPeriodByNameAsync(Guid organizationId, string name);

    Task<(List<SchedulingPeriod> Items, int TotalCount)> GetAllSchedulingPeriodsAsync(Guid organizationId, int page, int pageSize);

    Task UpdateSchedulingPeriodAsync(Guid organizationId, Guid schedulingPeriodId, string name, DateTime fromDate, DateTime toDate);

    Task DeleteSchedulingPeriodAsync(Guid organizationId, Guid schedulingPeriodId);
}
