using Chronos.Data.Context;
using Chronos.Domain.Resources;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Data.Repositories.Resources;

public class ResourceAttributeRepository(AppDbContext context) : IResourceAttributeRepository
{
    public async Task<ResourceAttribute?> GetByIdAsync(Guid id)
    {
        return await context.ResourceAttributes
            .FirstOrDefaultAsync(ra => ra.Id == id);
    }

    public async Task<List<ResourceAttribute>> GetAllAsync()
    {
        return await context.ResourceAttributes
            .ToListAsync();
    }

    public async Task AddAsync(ResourceAttribute resourceAttribute)
    {
        await context.ResourceAttributes.AddAsync(resourceAttribute);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ResourceAttribute resourceAttribute)
    {
        context.ResourceAttributes.Update(resourceAttribute);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(ResourceAttribute resourceAttribute)
    {
        context.ResourceAttributes.Remove(resourceAttribute);
        await context.SaveChangesAsync();
    }

    public async Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
    {
        var resourceAttributes = await context.ResourceAttributes
            .IgnoreQueryFilters()
            .Where(ra => ra.OrganizationId == organizationId)
            .ToListAsync(ct);
        context.ResourceAttributes.RemoveRange(resourceAttributes);
        await context.SaveChangesAsync(ct);
        return resourceAttributes.Count;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await context.ResourceAttributes
            .AnyAsync(ra => ra.Id == id);
    }
}