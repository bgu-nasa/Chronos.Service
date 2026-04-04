using Chronos.Data.Context;
using Chronos.Domain.Schedule;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Data.Repositories.Schedule;

public class UserConstraintRepository(AppDbContext context) : IUserConstraintRepository
{
    public async Task<UserConstraint?> GetByIdAsync(Guid id)
    {
        return await context.UserConstraints
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<UserConstraint>> GetAllAsync()
    {
        return await context.UserConstraints
            .OrderBy(c => c.Key)
            .ToListAsync();
    }

    public async Task<List<UserConstraint>> GetByUserPeriodAsync(Guid userId, Guid schedulingPeriodId)
    {
        return await context.UserConstraints
            .Where(c =>
                c.UserId == userId &&
                c.SchedulingPeriodId == schedulingPeriodId)
            .ToListAsync();
    }

    public async Task<List<UserConstraint>> GetByUserIdAsync(Guid userId)
    {
        return await context.UserConstraints
            .Where(c => c.UserId == userId)
            .ToListAsync();
    }

    public async Task<List<UserConstraint>> GetBySchedulingPeriodIdAsync(Guid schedulingPeriodId)
    {
        return await context.UserConstraints
            .Where(c => c.SchedulingPeriodId == schedulingPeriodId)
            .ToListAsync();
    }

    public async Task AddAsync(UserConstraint constraint)
    {
        await context.UserConstraints.AddAsync(constraint);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(UserConstraint constraint)
    {
        context.UserConstraints.Update(constraint);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(UserConstraint constraint)
    {
        context.UserConstraints.Remove(constraint);
        await context.SaveChangesAsync();
    }

    public async Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
    {
        var userConstraints = await context.UserConstraints
            .IgnoreQueryFilters()
            .Where(uc => uc.OrganizationId == organizationId)
            .ToListAsync(ct);
        context.UserConstraints.RemoveRange(userConstraints);
        await context.SaveChangesAsync(ct);
        return userConstraints.Count;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await context.UserConstraints
            .AnyAsync(c => c.Id == id);
    }
}
