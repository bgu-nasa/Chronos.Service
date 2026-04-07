using Chronos.Data.Context;
using Chronos.Domain.Resources;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Data.Repositories.Resources;

public class ActivityRepository(AppDbContext context) : IActivityRepository
{
    public async Task<Activity?> GetByIdAsync(Guid id)
    {
        return await context.Activities
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<List<Activity>> GetAllAsync()
    {
        return await context.Activities
            .OrderBy(a => a.SubjectId)
            .ToListAsync();
    }

    public async Task AddAsync(Activity activity)
    {
        await context.Activities.AddAsync(activity);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Activity activity)
    {
        context.Activities.Update(activity);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Activity activity)
    {
        context.ActivityConstraints.RemoveRange(context.ActivityConstraints.Where(ac => ac.ActivityId == activity.Id));
        context.Assignments.RemoveRange(context.Assignments.Where(a => a.ActivityId == activity.Id));
        context.Activities.Remove(activity);
        await context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await context.Activities
            .AnyAsync(a => a.Id == id);
    }
}