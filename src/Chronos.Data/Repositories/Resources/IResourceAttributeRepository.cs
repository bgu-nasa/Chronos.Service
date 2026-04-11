using Chronos.Data.Repositories.Common;
using Chronos.Domain.Resources;

namespace Chronos.Data.Repositories.Resources;

public interface IResourceAttributeRepository : IOrganizationScopedRepository
{
    Task<ResourceAttribute?> GetByIdAsync(Guid id);
    Task<List<ResourceAttribute>> GetAllAsync();
    Task AddAsync(ResourceAttribute resourceAttribute);
    Task UpdateAsync(ResourceAttribute resourceAttribute);
    Task DeleteAsync(ResourceAttribute resourceAttribute);
    Task<bool> ExistsAsync(Guid id);
}