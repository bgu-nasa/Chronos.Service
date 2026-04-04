using Chronos.Data.Context;
using Chronos.Domain.Schedule;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Data.Repositories.Schedule;

public class UserPreferenceRepository(AppDbContext context) : IUserPreferenceRepository
{
    public async Task<UserPreference?> GetByIdAsync(Guid id)
    {
        return await context.UserPreferences
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<UserPreference>> GetAllAsync()
    {
        return await context.UserPreferences
            .OrderBy(p => p.Key)
            .ToListAsync();
    }

    public async Task<List<UserPreference>> GetByUserPeriodAsync(Guid userId, Guid schedulingPeriodId)
    {
        return await context.UserPreferences
            .Where(p =>
                p.UserId == userId &&
                p.SchedulingPeriodId == schedulingPeriodId)
            .ToListAsync();
    }

    public async Task<List<UserPreference>> GetByUserIdAsync(Guid userId)
    {
        return await context.UserPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync();
    }

    public async Task<List<UserPreference>> GetBySchedulingPeriodIdAsync(Guid schedulingPeriodId)
    {
        return await context.UserPreferences
            .Where(p => p.SchedulingPeriodId == schedulingPeriodId)
            .ToListAsync();
    }

    public async Task AddAsync(UserPreference preference)
    {
        await context.UserPreferences.AddAsync(preference);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(UserPreference preference)
    {
        context.UserPreferences.Update(preference);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(UserPreference preference)
    {
        context.UserPreferences.Remove(preference);
        await context.SaveChangesAsync();
    }

    public async Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
    {
        var userPreferences = await context.UserPreferences
            .IgnoreQueryFilters()
            .Where(up => up.OrganizationId == organizationId)
            .ToListAsync(ct);
        context.UserPreferences.RemoveRange(userPreferences);
        await context.SaveChangesAsync(ct);
        return userPreferences.Count;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await context.UserPreferences
            .AnyAsync(p => p.Id == id);
    }
}
