using Chronos.Data.Context;
using Chronos.Domain.Resources;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Data.Repositories.Resources;

public class ResourceRepository(AppDbContext context) : IResourceRepository
{
    public async Task<Resource?> GetByIdAsync(Guid id)
    {
        return await context.Resources
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<List<Resource>> GetAllAsync()
    {
        return await context.Resources
            .ToListAsync();
    }

    public async Task AddAsync(Resource resource)
    {
        await context.Resources.AddAsync(resource);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Resource resource)
    {
        context.Resources.Update(resource);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Resource resource)
    {
        context.ResourceAttributeAssignments.RemoveRange(context.ResourceAttributeAssignments.Where(raa => raa.ResourceId == resource.Id));
        context.Assignments.RemoveRange(context.Assignments.Where(a => a.ResourceId == resource.Id));
        context.Resources.Remove(resource);
        await context.SaveChangesAsync();
    }

    public async Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
    {
        var resources = await context.Resources
            .IgnoreQueryFilters()
            .Where(r => r.OrganizationId == organizationId)
            .ToListAsync(ct);
        context.Resources.RemoveRange(resources);
        await context.SaveChangesAsync(ct);
        return resources.Count;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await context.Resources
            .AnyAsync(r => r.Id == id);
    }
}