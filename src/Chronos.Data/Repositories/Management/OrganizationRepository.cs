using Chronos.Data.Context;
using Chronos.Domain.Management;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Data.Repositories.Management;

public class OrganizationRepository(AppDbContext context) : IOrganizationRepository
{
    public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Organizations
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
        return await context.Organizations
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