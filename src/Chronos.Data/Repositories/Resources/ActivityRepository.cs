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
        context.Activities.Remove(activity);
        await context.SaveChangesAsync();
    }

    public async Task DeleteBySubjectIdAsync(Guid subjectId)
    {
        var activities = await context.Activities
            .Where(a => a.SubjectId == subjectId)
            .ToListAsync();
        context.Activities.RemoveRange(activities);
        await context.SaveChangesAsync();
    }

    public async Task DeleteByAssignedUserIdAsync(Guid assignedUserId)
    {
        var activities = await context.Activities
            .Where(a => a.AssignedUserId == assignedUserId)
            .ToListAsync();
        context.Activities.RemoveRange(activities);
        await context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await context.Activities
            .AnyAsync(a => a.Id == id);
    }
}