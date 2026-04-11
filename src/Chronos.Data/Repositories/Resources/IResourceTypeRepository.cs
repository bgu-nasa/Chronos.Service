using Chronos.Domain.Resources;

namespace Chronos.Data.Repositories.Resources;

public interface IResourceTypeRepository
{
    Task<ResourceType?> GetByIdAsync(Guid id);
    Task<List<ResourceType>> GetAllAsync();
    Task AddAsync(ResourceType resourceType);
    Task UpdateAsync(ResourceType resourceType);
    Task DeleteAsync(ResourceType resourceType);
    Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id);
}