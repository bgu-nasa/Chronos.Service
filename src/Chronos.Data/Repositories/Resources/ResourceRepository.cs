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
        context.Resources.Remove(resource);
        await context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await context.Resources
            .AnyAsync(r => r.Id == id);
    }

    public async Task DeleteByResourceTypeIdAsync(Guid resourceTypeId)
    {
        var resources = await context.Resources
            .Where(r => r.ResourceTypeId == resourceTypeId)
            .ToListAsync();
        context.Resources.RemoveRange(resources);
        await context.SaveChangesAsync();
    }
}