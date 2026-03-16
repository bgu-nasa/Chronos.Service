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

    public async Task<List<Assignment>> GetAllAsync()
    {
        return await context.Assignments
            .ToListAsync();
    }

    public async Task<List<Assignment>> GetBySlotIdAsync(Guid slotId)
    {
        return await context.Assignments
            .Where(a => a.SlotId == slotId)
            .ToListAsync();
    }

    public async Task<List<Assignment>> GetByActivityIdAsync(Guid activityId)
    {
        return await context.Assignments
            .Where(a => a.ActivityId == activityId)
            .ToListAsync();
    }
    
    public async Task<Assignment?> GetBySlotIdAndResourceIdAsync(Guid slotId, Guid resourceId)
    {
        return await context.Assignments
            .FirstOrDefaultAsync(a => a.SlotId == slotId && a.ResourceId == resourceId);
    }
    public async Task<List<Assignment>> GetByResourceIdAsync(Guid resourceId)
    {
        return await context.Assignments
            .Where(a => a.ResourceId == resourceId)
            .ToListAsync();
    }

    public async Task<List<Assignment>> GetBySchedulingPeriodIdAsync(Guid schedulingPeriodId)
    {
        var slotIds = await context.Slots
            .Where(s => s.SchedulingPeriodId == schedulingPeriodId)
            .Select(s => s.Id)
            .ToListAsync();
        return await context.Assignments
            .Where(a => slotIds.Contains(a.SlotId))
            .ToListAsync();
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

    public async Task DeleteBySchedulingPeriodIdAsync(Guid schedulingPeriodId)
    {
        var assignments = await GetBySchedulingPeriodIdAsync(schedulingPeriodId);
        context.Assignments.RemoveRange(assignments);
        await context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await context.Assignments
            .AnyAsync(a => a.Id == id);
    }
}
