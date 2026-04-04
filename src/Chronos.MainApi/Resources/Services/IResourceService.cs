using Chronos.Domain.Resources;

namespace Chronos.MainApi.Resources.Services;

public interface IResourceService
{
    Task<Resource> CreateResourceAsync(Guid id, Guid organizationId, Guid resourceTypeId, string location, string identifier, int? capacity);
    Task<Resource> GetResourceAsync(Guid organizationId, Guid resourceId);
    Task<List<Resource>> GetResourcesAsync(Guid organizationId);
    Task UpdateResourceAsync(Guid organizationId, Guid resourceId, Guid resourceTypeId, string location, string identifier, int? capacity);
    Task DeleteResourceAsync(Guid organizationId, Guid resourceId);
    Task DeleteResourcesByResourceTypeIdAsync(Guid organizationId, Guid resourceTypeId);
}