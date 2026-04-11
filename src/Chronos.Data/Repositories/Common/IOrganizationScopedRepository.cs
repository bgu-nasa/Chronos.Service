namespace Chronos.Data.Repositories.Common;

public interface IOrganizationScopedRepository
{
    Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);
}
