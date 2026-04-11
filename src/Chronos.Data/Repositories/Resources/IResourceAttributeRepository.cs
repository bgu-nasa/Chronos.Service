using Chronos.Domain.Resources;

namespace Chronos.Data.Repositories.Resources;

public interface IResourceAttributeRepository
{
    Task<ResourceAttribute?> GetByIdAsync(Guid id);
    Task<List<ResourceAttribute>> GetAllAsync();
    Task AddAsync(ResourceAttribute resourceAttribute);
    Task UpdateAsync(ResourceAttribute resourceAttribute);
    Task DeleteAsync(ResourceAttribute resourceAttribute);
    Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id);
}