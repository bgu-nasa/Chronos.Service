using Chronos.Domain.Schedule;

namespace Chronos.Data.Repositories.Schedule;

public interface IOrganizationPolicyRepository
{
    Task<OrganizationPolicy?> GetByIdAsync(Guid id);

    Task<List<OrganizationPolicy>> GetAllAsync();

    Task<List<OrganizationPolicy>> GetByPeriodAsync(Guid schedulingPeriodId);

    Task AddAsync(OrganizationPolicy policy);

    Task UpdateAsync(OrganizationPolicy policy);

    Task DeleteAsync(OrganizationPolicy policy);
    Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);

    Task<bool> ExistsAsync(Guid id);
}

