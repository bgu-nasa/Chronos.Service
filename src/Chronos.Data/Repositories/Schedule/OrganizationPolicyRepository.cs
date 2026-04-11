using Chronos.Data.Context;
using Chronos.Domain.Schedule;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Data.Repositories.Schedule;

public class OrganizationPolicyRepository(AppDbContext context) : IOrganizationPolicyRepository
{
    public async Task<OrganizationPolicy?> GetByIdAsync(Guid id)
    {
        return await context.OrganizationPolicies
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<OrganizationPolicy>> GetAllAsync()
    {
        return await context.OrganizationPolicies
            .OrderBy(p => p.Key)
            .ToListAsync();
    }

    public async Task<List<OrganizationPolicy>> GetByPeriodAsync(Guid schedulingPeriodId)
    {
        return await context.OrganizationPolicies
            .Where(p => p.SchedulingPeriodId == schedulingPeriodId)
            .ToListAsync();
    }

    public async Task AddAsync(OrganizationPolicy policy)
    {
        await context.OrganizationPolicies.AddAsync(policy);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(OrganizationPolicy policy)
    {
        context.OrganizationPolicies.Update(policy);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(OrganizationPolicy policy)
    {
        context.OrganizationPolicies.Remove(policy);
        await context.SaveChangesAsync();
    }

    public async Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
    {
        var organizationPolicies = await context.OrganizationPolicies
            .IgnoreQueryFilters()
            .Where(op => op.OrganizationId == organizationId)
            .ToListAsync(ct);
        context.OrganizationPolicies.RemoveRange(organizationPolicies);
        await context.SaveChangesAsync(ct);
        return organizationPolicies.Count;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await context.OrganizationPolicies
            .AnyAsync(p => p.Id == id);
    }
}
