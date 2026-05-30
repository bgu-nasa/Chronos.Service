using Chronos.Admin.Organizations.Contracts;
using Chronos.Data.Context;
using Chronos.Domain.Management.Roles;
using Chronos.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Admin.Organizations;

public class AdminOrganizationService(AppDbContext context) : IAdminOrganizationService
{
    public async Task<IReadOnlyList<OrgSummary>> ListOrganizationsAsync(
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var organizations = await context.Organizations
            .IgnoreQueryFilters()
            .Where(o => includeDeleted || !o.Deleted)
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);

        if (organizations.Count == 0)
        {
            return [];
        }

        var orgIds = organizations.Select(o => o.Id).ToList();
        var adminEmailsByOrg = await GetAdminEmailsByOrganizationAsync(orgIds, cancellationToken);
        var userCountsByOrg = await GetUserCountsByOrganizationAsync(orgIds, cancellationToken);

        return organizations
            .Select(o => new OrgSummary(
                o.Id,
                o.Name,
                adminEmailsByOrg.GetValueOrDefault(o.Id, []),
                userCountsByOrg.GetValueOrDefault(o.Id, 0),
                o.CreatedAt))
            .ToList();
    }

    public async Task<OrgSummary> GetOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var organization = await context.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

        if (organization is null)
        {
            throw new NotFoundException($"Organization '{organizationId}' was not found.");
        }

        var adminEmails = await GetAdminEmailsByOrganizationAsync([organizationId], cancellationToken);
        var userCounts = await GetUserCountsByOrganizationAsync([organizationId], cancellationToken);

        return new OrgSummary(
            organization.Id,
            organization.Name,
            adminEmails.GetValueOrDefault(organizationId, []),
            userCounts.GetValueOrDefault(organizationId, 0),
            organization.CreatedAt);
    }

    private async Task<Dictionary<Guid, List<string>>> GetAdminEmailsByOrganizationAsync(
        IReadOnlyList<Guid> organizationIds,
        CancellationToken cancellationToken)
    {
        var assignments = await context.RoleAssignments
            .IgnoreQueryFilters()
            .Where(ra => organizationIds.Contains(ra.OrganizationId) && ra.Role == Role.Administrator)
            .Select(ra => new { ra.OrganizationId, ra.UserId })
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
        {
            return new Dictionary<Guid, List<string>>();
        }

        var userIds = assignments.Select(a => a.UserId).Distinct().ToList();
        var users = await context.Users
            .IgnoreQueryFilters()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(cancellationToken);

        var emailByUserId = users.ToDictionary(u => u.Id, u => u.Email);

        return assignments
            .Where(a => emailByUserId.ContainsKey(a.UserId))
            .GroupBy(a => a.OrganizationId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(a => emailByUserId[a.UserId]).Distinct().OrderBy(e => e).ToList());
    }

    private async Task<Dictionary<Guid, int>> GetUserCountsByOrganizationAsync(
        IReadOnlyList<Guid> organizationIds,
        CancellationToken cancellationToken)
    {
        return await context.Users
            .IgnoreQueryFilters()
            .Where(u => organizationIds.Contains(u.OrganizationId))
            .GroupBy(u => u.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.Count, cancellationToken);
    }
}
