using Chronos.Data.Context;
using Chronos.Domain.Schedule;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Data.Repositories.Schedule;

public class ActivityConstraintRepository(AppDbContext context) : IActivityConstraintRepository
{
    public async Task<ActivityConstraint?> GetByIdAsync(Guid id)
    {
        return await context.ActivityConstraints
        .FirstOrDefaultAsync(ac => ac.Id == id);
    }

    public async Task<List<ActivityConstraint>> GetAllAsync()
    {
        return await context.ActivityConstraints.ToListAsync();
    }

    public async Task<List<ActivityConstraint>> GetByActivityIdAsync(Guid activityId)
    {
        return await context.ActivityConstraints
            .Where(ac => ac.ActivityId == activityId)
            .ToListAsync();
    }

    public async Task AddAsync(ActivityConstraint constraint)
    {
        await context.ActivityConstraints.AddAsync(constraint);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ActivityConstraint constraint)
    {
        context.ActivityConstraints.Update(constraint);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(ActivityConstraint constraint)
    {
        context.ActivityConstraints.Remove(constraint);
        await context.SaveChangesAsync();
    }

    public async Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
    {
        var activityConstraints = await context.ActivityConstraints
            .IgnoreQueryFilters()
            .Where(ac => ac.OrganizationId == organizationId)
            .ToListAsync(ct);
        context.ActivityConstraints.RemoveRange(activityConstraints);
        await context.SaveChangesAsync(ct);
        return activityConstraints.Count;
    }

    public async Task<int> DeleteAllByDepartmentIdAsync(Guid departmentId, CancellationToken ct = default)
    {
        var activityIds = await context.Subjects
            .IgnoreQueryFilters()
            .Where(s => s.DepartmentId == departmentId)
            .Join(context.Activities.IgnoreQueryFilters(),
                s => s.Id, a => a.SubjectId, (s, a) => a.Id)
            .ToListAsync(ct);
        var activityConstraints = await context.ActivityConstraints
            .IgnoreQueryFilters()
            .Where(ac => activityIds.Contains(ac.ActivityId))
            .ToListAsync(ct);
        context.ActivityConstraints.RemoveRange(activityConstraints);
        await context.SaveChangesAsync(ct);
        return activityConstraints.Count;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await context.ActivityConstraints.AnyAsync(ac => ac.Id == id);
    }
}
