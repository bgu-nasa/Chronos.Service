using Chronos.Data.Context;
using Chronos.Domain.Schedule;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Data.Repositories.Schedule;

public class AppealRepository(AppDbContext context) : IAppealRepository
{
    public async Task<Appeal?> GetByIdAsync(Guid id)
    {
        return await context.Appeals
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<List<Appeal>> GetAllAsync()
    {
        return await context.Appeals.ToListAsync();
    }

    public async Task<List<Appeal>> GetByAssignmentIdAsync(Guid assignmentId)
    {
        return await context.Appeals
            .Where(a => a.AssignmentId == assignmentId)
            .ToListAsync();
    }

    public async Task AddAsync(Appeal appeal)
    {
        await context.Appeals.AddAsync(appeal);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Appeal appeal)
    {
        context.Appeals.Update(appeal);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Appeal appeal)
    {
        context.Appeals.Remove(appeal);
        await context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await context.Appeals.AnyAsync(a => a.Id == id);
    }

    public async Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
    {
        var appeals = await context.Appeals
            .IgnoreQueryFilters()
            .Where(a => a.OrganizationId == organizationId)
            .ToListAsync(ct);
        context.Appeals.RemoveRange(appeals);
        await context.SaveChangesAsync(ct);
        return appeals.Count;
    }
}
