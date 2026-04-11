using Chronos.Data.Context;
using Chronos.Domain.Resources;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Data.Repositories.Resources;

public class ResourceTypeRepository(AppDbContext context) : IResourceTypeRepository
{
    public async Task<ResourceType?> GetByIdAsync(Guid id)
    {
        return await context.ResourceTypes
            .FirstOrDefaultAsync(rt => rt.Id == id);
    }

    public async Task<List<ResourceType>> GetAllAsync()
    {
        return await context.ResourceTypes
            .ToListAsync();
    }

    public async Task AddAsync(ResourceType resourceType)
    {
        await context.ResourceTypes.AddAsync(resourceType);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ResourceType resourceType)
    {
        context.ResourceTypes.Update(resourceType);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(ResourceType resourceType)
    {
        context.ResourceTypes.Remove(resourceType);
        await context.SaveChangesAsync();
    }

    public async Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
    {
        var resourceTypes = await context.ResourceTypes
            .IgnoreQueryFilters()
            .Where(rt => rt.OrganizationId == organizationId)
            .ToListAsync(ct);
        context.ResourceTypes.RemoveRange(resourceTypes);
        await context.SaveChangesAsync(ct);
        return resourceTypes.Count;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await context.ResourceTypes
            .AnyAsync(rt => rt.Id == id);
    }
}