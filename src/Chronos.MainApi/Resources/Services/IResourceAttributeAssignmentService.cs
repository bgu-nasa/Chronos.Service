using Chronos.Domain.Resources;

namespace Chronos.MainApi.Resources.Services;

public interface IResourceAttributeAssignmentService
{
    Task<ResourceAttributeAssignment> CreateResourceAttributeAssignmentAsync(Guid resourceId, Guid resourceAttributeId, Guid organizationId);
    Task<ResourceAttributeAssignment> GetResourceAttributeAssignmentAsync(Guid resourceId, Guid resourceAttributeId, Guid organizationId);
    Task<List<ResourceAttributeAssignment>> GetAllResourceAttributeAssignmentsAsync(Guid organizationId);
    Task UpdateResourceAttributeAssignmentAsync(Guid resourceId, Guid resourceAttributeId, Guid organizationId);
    Task DeleteResourceAttributeAssignmentAsync(Guid resourceId, Guid resourceAttributeId, Guid organizationId);
    Task DeleteResourceAttributeAssignmentsByResourceIdAsync(Guid organizationId, Guid resourceId);
    Task DeleteResourceAttributeAssignmentsByResourceAttributeIdAsync(Guid organizationId, Guid resourceAttributeId);
}