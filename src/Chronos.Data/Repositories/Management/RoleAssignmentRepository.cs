using Chronos.Data.Context;
using Chronos.Domain.Management.Roles;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Data.Repositories.Management;

public class RoleAssignmentRepository(AppDbContext context) : IRoleAssignmentRepository
{
    public async Task<List<RoleAssignment>> GetUserAssignmentsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.RoleAssignments
            .Where(ra => ra.OrganizationId == organizationId && ra.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<RoleAssignment>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await context.RoleAssignments
            .Where(ra => ra.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);
    }

    public async Task<RoleAssignment?> GetAsync(Guid organizationId, Guid roleAssignmentId, CancellationToken cancellationToken = default)
    {
        return await context.RoleAssignments
            .FirstOrDefaultAsync(ra => ra.OrganizationId == organizationId && ra.Id == roleAssignmentId, cancellationToken);
    }

    public async Task<RoleAssignment> AddAsync(RoleAssignment roleAssignment, CancellationToken cancellationToken = default)
    {
        await context.RoleAssignments.AddAsync(roleAssignment, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return roleAssignment;
    }

    public async Task DeleteAsync(Guid organizationId, Guid roleAssignmentId, CancellationToken cancellationToken = default)
    {
        var roleAssignment = await GetAsync(organizationId, roleAssignmentId, cancellationToken);
        if (roleAssignment is not null)
        {
            context.RoleAssignments.Remove(roleAssignment);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
    {
        var roleAssignments = await context.RoleAssignments
            .IgnoreQueryFilters()
            .Where(ra => ra.OrganizationId == organizationId)
            .ToListAsync(ct);
        context.RoleAssignments.RemoveRange(roleAssignments);
        await context.SaveChangesAsync(ct);
        return roleAssignments.Count;
    }

    public async Task<int> DeleteAllByDepartmentIdAsync(Guid departmentId, CancellationToken ct = default)
    {
        var roleAssignments = await context.RoleAssignments
            .IgnoreQueryFilters()
            .Where(ra => ra.DepartmentId == departmentId)
            .ToListAsync(ct);
        context.RoleAssignments.RemoveRange(roleAssignments);
        await context.SaveChangesAsync(ct);
        return roleAssignments.Count;
    }
}