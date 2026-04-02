using Chronos.Data.Context;
using Chronos.Domain.Schedule;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Data.Repositories.Schedule;

public class AssignmentRepository(AppDbContext context) : IAssignmentRepository
{
    public async Task<Assignment?> GetByIdAsync(Guid id)
    {
        return await context.Assignments
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<(List<Assignment> Items, int TotalCount)> GetAllAsync(int page, int pageSize)
    {
        var query = context.Assignments.OrderBy(a => a.Id);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public async Task<(List<Assignment> Items, int TotalCount)> GetBySlotIdAsync(Guid slotId, int page, int pageSize)
    {
        var query = context.Assignments
            .Where(a => a.SlotId == slotId)
            .OrderBy(a => a.Id);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public async Task<(List<Assignment> Items, int TotalCount)> GetByActivityIdAsync(Guid activityId, int page, int pageSize)
    {
        var query = context.Assignments
            .Where(a => a.ActivityId == activityId)
            .OrderBy(a => a.Id);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public async Task<Assignment?> GetBySlotIdAndResourceIdAsync(Guid slotId, Guid resourceId)
    {
        return await context.Assignments
            .FirstOrDefaultAsync(a => a.SlotId == slotId && a.ResourceId == resourceId);
    }

    public async Task<List<Assignment>> GetByResourceIdAsync(Guid resourceId)
    {
        var query = context.Assignments
            .Where(a => a.ResourceId == resourceId)
            .OrderBy(a => a.Id);
        var items = await query.ToListAsync();
        return items;
    }


    public async Task AddAsync(Assignment assignment)
    {
        await context.Assignments.AddAsync(assignment);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Assignment assignment)
    {
        context.Assignments.Update(assignment);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Assignment assignment)
    {
        context.Assignments.Remove(assignment);
        await context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await context.Assignments
            .AnyAsync(a => a.Id == id);
    }
}
