using Chronos.Domain.Schedule;

namespace Chronos.MainApi.Schedule.Services;

public interface ISlotService
{
    Task<Guid> CreateSlotAsync(Guid organizationId, Guid schedulingPeriodId, WeekDays weekday, TimeSpan fromTime, TimeSpan toTime);

    Task<Slot> GetSlotAsync(Guid organizationId, Guid slotId);

    Task<(List<Slot> Items, int TotalCount)> GetAllSlotsAsync(Guid organizationId, int page, int pageSize);
    
    Task<(List<Slot> Items, int TotalCount)> GetSlotsBySchedulingPeriodAsync(Guid organizationId, Guid schedulingPeriodId, int page, int pageSize);

    Task UpdateSlotAsync(Guid organizationId, Guid slotId, WeekDays weekday, TimeSpan fromTime, TimeSpan toTime);

    Task DeleteSlotAsync(Guid organizationId, Guid slotId);
}
