using Chronos.Domain.Resources;

namespace Chronos.Data.Repositories.Resources;

public interface IResourceRepository
{
    Task<Resource?> GetByIdAsync(Guid id);
    Task<List<Resource>> GetAllAsync();
    Task AddAsync(Resource resource);
    Task UpdateAsync(Resource resource);
    Task DeleteAsync(Resource resource);
    Task<bool> ExistsAsync(Guid id);
    Task DeleteByResourceTypeIdAsync(Guid resourceTypeId);
}