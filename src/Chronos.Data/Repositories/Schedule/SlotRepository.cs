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

    public async Task<List<Slot>> GetBySchedulingPeriodIdAsync(Guid schedulingPeriodId)
    {
        return await context.Slots
            .Where(s => s.SchedulingPeriodId == schedulingPeriodId)
            .ToListAsync();
    }

    public async Task<List<Slot>> GetAllAsync()
    {
        return await context.Slots
            .OrderBy(s => s.Weekday)
            .ThenBy(s => s.FromTime)
            .ToListAsync();
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

    public async Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
    {
        var slots = await context.Slots
            .IgnoreQueryFilters()
            .Where(s => s.OrganizationId == organizationId)
            .ToListAsync(ct);
        context.Slots.RemoveRange(slots);
        await context.SaveChangesAsync(ct);
        return slots.Count;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await context.Slots
            .AnyAsync(s => s.Id == id);
    }
}
