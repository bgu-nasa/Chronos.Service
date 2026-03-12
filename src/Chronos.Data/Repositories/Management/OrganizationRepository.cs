using Chronos.Data.Context;
using Chronos.Domain.Management;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Data.Repositories.Management;

public class OrganizationRepository(AppDbContext context) : IOrganizationRepository
{
    public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Ignore query filters when looking up organization by ID
        // This is needed for authentication flows where organization context isn't set yet
        return await context.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task AddAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        await context.Organizations.AddAsync(organization, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        context.Organizations.Update(organization);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        context.Organizations.Remove(organization);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Ignore query filters when checking organization existence
        // This is needed for authentication flows where organization context isn't set yet
        return await context.Organizations
            .IgnoreQueryFilters()
            .AnyAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<List<Organization>> GetAllDeletedAsync(CancellationToken cancellationToken = default)
    {
        return await context.Organizations
            .IgnoreQueryFilters()
            .Where(o => o.Deleted == true)
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);
    }
}