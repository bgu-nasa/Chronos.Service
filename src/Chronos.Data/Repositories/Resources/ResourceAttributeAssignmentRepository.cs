using Chronos.Data.Context;
using Chronos.Domain.Resources;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Data.Repositories.Resources;

public class ResourceAttributeAssignmentRepository(AppDbContext context) : IResourceAttributeAssignmentRepository
{
    public async Task<ResourceAttributeAssignment?> GetByIdAsync(Guid resourceId, Guid resourceAttributeId)
    {
        return await context.ResourceAttributeAssignments
            .FirstOrDefaultAsync(raa => raa.ResourceId == resourceId && raa.ResourceAttributeId == resourceAttributeId);
    }

    public async Task<List<ResourceAttributeAssignment>> GetAllAsync()
    {
        return await context.ResourceAttributeAssignments
            .ToListAsync();
    }

    public async Task AddAsync(ResourceAttributeAssignment resourceAttributeAssignment)
    {
        await context.ResourceAttributeAssignments.AddAsync(resourceAttributeAssignment);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ResourceAttributeAssignment resourceAttributeAssignment)
    {
        context.ResourceAttributeAssignments.Update(resourceAttributeAssignment);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(ResourceAttributeAssignment resourceAttributeAssignment)
    {
        context.ResourceAttributeAssignments.Remove(resourceAttributeAssignment);
        await context.SaveChangesAsync();
    }

    public async Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
    {
        var resourceAttributeAssignments = await context.ResourceAttributeAssignments
            .IgnoreQueryFilters()
            .Where(raa => raa.OrganizationId == organizationId)
            .ToListAsync(ct);
        context.ResourceAttributeAssignments.RemoveRange(resourceAttributeAssignments);
        await context.SaveChangesAsync(ct);
        return resourceAttributeAssignments.Count;
    }

    public async Task<bool> ExistsAsync(Guid resourceId, Guid resourceAttributeId)
    {
        return await context.ResourceAttributeAssignments
            .AnyAsync(raa => raa.ResourceId == resourceId && raa.ResourceAttributeId == resourceAttributeId);
    }
}