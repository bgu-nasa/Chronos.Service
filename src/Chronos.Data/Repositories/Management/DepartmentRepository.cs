using Chronos.Data.Context;
using Chronos.Domain.Management;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Data.Repositories.Management;

public class DepartmentRepository(AppDbContext context) : IDepartmentRepository
{
    public async Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Departments
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<List<Department>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Departments
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Department department, CancellationToken cancellationToken = default)
    {
        await context.Departments.AddAsync(department, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Department department, CancellationToken cancellationToken = default)
    {
        context.Departments.Update(department);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Department department, CancellationToken cancellationToken = default)
    {
        context.Departments.Remove(department);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
    {
        var departments = await context.Departments
            .IgnoreQueryFilters()
            .Where(d => d.OrganizationId == organizationId)
            .ToListAsync(ct);
        context.Departments.RemoveRange(departments);
        await context.SaveChangesAsync(ct);
        return departments.Count;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Departments
            .AnyAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<List<Department>> GetAllDeletedAsync(CancellationToken cancellationToken = default)
    {
        return await context.Departments
            .IgnoreQueryFilters()
            .Where(d => d.Deleted == true)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }
}