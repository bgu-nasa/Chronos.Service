using Chronos.Data.Repositories.Common;
using Chronos.Domain.Resources;

namespace Chronos.Data.Repositories.Resources;

public interface IResourceTypeRepository : IOrganizationScopedRepository
{
    Task<ResourceType?> GetByIdAsync(Guid id);
    Task<List<ResourceType>> GetAllAsync();
    Task AddAsync(ResourceType resourceType);
    Task UpdateAsync(ResourceType resourceType);
    Task DeleteAsync(ResourceType resourceType);
    Task<bool> ExistsAsync(Guid id);
}