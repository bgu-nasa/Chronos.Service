using Chronos.Domain.Resources;

namespace Chronos.Data.Repositories.Resources;

public interface IResourceAttributeAssignmentRepository
{
    Task<ResourceAttributeAssignment?> GetByIdAsync(Guid resourceId, Guid resourceAttributeId);
    Task<List<ResourceAttributeAssignment>> GetAllAsync();
    Task AddAsync(ResourceAttributeAssignment resourceAttributeAssignment);
    Task UpdateAsync(ResourceAttributeAssignment resourceAttributeAssignment);
    Task DeleteAsync(ResourceAttributeAssignment resourceAttributeAssignment);
    Task<bool> ExistsAsync(Guid resourceId, Guid resourceAttributeId);
    Task DeleteByResourceIdAsync(Guid resourceId);
    Task DeleteByResourceAttributeIdAsync(Guid resourceAttributeId);
}