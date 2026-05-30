using Chronos.Admin.Organizations.Contracts;

namespace Chronos.Admin.Organizations;

public interface IAdminOrganizationService
{
    Task<IReadOnlyList<OrgSummary>> ListOrganizationsAsync(
        bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    Task<OrgSummary> GetOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
}
