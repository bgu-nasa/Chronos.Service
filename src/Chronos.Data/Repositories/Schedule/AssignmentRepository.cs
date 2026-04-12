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

    public async Task<List<Assignment>> GetAllAsync(AssignmentQuery? query = null)
    {
        var q = context.Assignments.AsQueryable();

        if (query is not null)
        {
            if (query.OrganizationId.HasValue)
                q = q.Where(a => a.OrganizationId == query.OrganizationId.Value);

            if (query.SlotId.HasValue)
                q = q.Where(a => a.SlotId == query.SlotId.Value);

            if (query.ResourceId.HasValue)
                q = q.Where(a => a.ResourceId == query.ResourceId.Value);

            if (query.ActivityId.HasValue)
                q = q.Where(a => a.ActivityId == query.ActivityId.Value);

            if (query.SchedulingPeriodId.HasValue)
            {
                var slotIds = await context.Slots
                    .Where(s => s.SchedulingPeriodId == query.SchedulingPeriodId.Value)
                    .Select(s => s.Id)
                    .ToListAsync();
                q = q.Where(a => slotIds.Contains(a.SlotId));
            }

            if (query.UserId.HasValue)
            {
                var activityIds = await context.Activities
                    .Where(a => a.AssignedUserId == query.UserId.Value)
                    .Select(a => a.Id)
                    .ToListAsync();
                q = q.Where(a => activityIds.Contains(a.ActivityId));
            }
        }

        return await q.ToListAsync();
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

    public async Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
    {
        var assignments = await context.Assignments
            .IgnoreQueryFilters()
            .Where(a => a.OrganizationId == organizationId)
            .ToListAsync(ct);
        context.Assignments.RemoveRange(assignments);
        await context.SaveChangesAsync(ct);
        return assignments.Count;
    }

    public async Task<int> DeleteAllByDepartmentIdAsync(Guid departmentId, CancellationToken ct = default)
    {
        var activityIds = await context.Subjects
            .IgnoreQueryFilters()
            .Where(s => s.DepartmentId == departmentId)
            .Join(context.Activities.IgnoreQueryFilters(),
                s => s.Id, a => a.SubjectId, (s, a) => a.Id)
            .ToListAsync(ct);
        var assignments = await context.Assignments
            .IgnoreQueryFilters()
            .Where(a => activityIds.Contains(a.ActivityId))
            .ToListAsync(ct);
        context.Assignments.RemoveRange(assignments);
        await context.SaveChangesAsync(ct);
        return assignments.Count;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await context.Assignments
            .AnyAsync(a => a.Id == id);
    }
}
