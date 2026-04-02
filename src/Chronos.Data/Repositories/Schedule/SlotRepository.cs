using Chronos.Data.Context;
using Chronos.Domain.Schedule;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Data.Repositories.Schedule;

public class SlotRepository(AppDbContext context) : ISlotRepository
{
    public async Task<Slot?> GetByIdAsync(Guid id)
    {
        return await context.Slots
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<(List<Slot> Items, int TotalCount)> GetBySchedulingPeriodIdAsync(Guid schedulingPeriodId, int page, int pageSize)
    {
        var query = context.Slots
            .Where(s => s.SchedulingPeriodId == schedulingPeriodId)
            .OrderBy(s => s.Weekday)
            .ThenBy(s => s.FromTime);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public async Task<(List<Slot> Items, int TotalCount)> GetAllAsync(int page, int pageSize)
    {
        var query = context.Slots
            .OrderBy(s => s.Weekday)
            .ThenBy(s => s.FromTime);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public async Task AddAsync(Slot slot)
    {
        await context.Slots.AddAsync(slot);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Slot slot)
    {
        context.Slots.Update(slot);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Slot slot)
    {
        context.Slots.Remove(slot);
        await context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await context.Slots
            .AnyAsync(s => s.Id == id);
    }
}
