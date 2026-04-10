using Chronos.Data.Repositories.Resources;
using Chronos.Domain.Resources;
using Chronos.Shared.Exceptions;

namespace Chronos.MainApi.Resources.Services;

public class ResourceAttributeAssignmentService(
    IResourceAttributeAssignmentRepository resourceAttributeAssignmentRepository,
    ResourceValidationService validationService,
    ILogger<ResourceAttributeAssignmentService> logger) : IResourceAttributeAssignmentService
{
    public async Task<ResourceAttributeAssignment> CreateResourceAttributeAssignmentAsync(Guid resourceId, Guid resourceAttributeId, Guid organizationId)
    {
        logger.LogInformation("Creating resource attribute assignment. OrganizationId: {OrganizationId}, ResourceId: {ResourceId}, ResourceAttributeId: {ResourceAttributeId}",
            organizationId, resourceId, resourceAttributeId);

        await validationService.ValidationOrganizationAsync(organizationId);

        if (await resourceAttributeAssignmentRepository.ExistsAsync(resourceId, resourceAttributeId))
        {
            logger.LogWarning("Resource attribute assignment already exists. ResourceId: {ResourceId}, ResourceAttributeId: {ResourceAttributeId}, OrganizationId: {OrganizationId}",
                resourceId, resourceAttributeId, organizationId);
            throw new BadRequestException("Resource attribute assignment already exists for this resource and attribute pair.");
        }

        var resourceAttributeAssignment = new ResourceAttributeAssignment
        {
            ResourceId = resourceId,
            ResourceAttributeId = resourceAttributeId,
            OrganizationId = organizationId
        };

        await resourceAttributeAssignmentRepository.AddAsync(resourceAttributeAssignment);

        logger.LogInformation("Resource attribute assignment created successfully. ResourceId: {ResourceId}, ResourceAttributeId: {ResourceAttributeId}, OrganizationId: {OrganizationId}", resourceId, resourceAttributeId, organizationId);
        return resourceAttributeAssignment;
    }

    public async Task<ResourceAttributeAssignment> GetResourceAttributeAssignmentAsync(Guid resourceId, Guid resourceAttributeId, Guid organizationId)
    {
        logger.LogDebug("Retrieving resource attribute assignment. OrganizationId: {OrganizationId}, ResourceId: {ResourceId}, ResourceAttributeId: {ResourceAttributeId}", organizationId, resourceId, resourceAttributeId);

        await validationService.ValidationOrganizationAsync(organizationId);
        var resourceAttributeAssignment = await validationService.ValidateAndGetResourceAttributeAssignmentAsync(organizationId, resourceId, resourceAttributeId);
        return resourceAttributeAssignment;
    }

    public async Task<List<ResourceAttributeAssignment>> GetAllResourceAttributeAssignmentsAsync(Guid organizationId)
    {
        logger.LogDebug("Retrieving all resource attribute assignments for organization. OrganizationId: {OrganizationId}", organizationId);

        await validationService.ValidationOrganizationAsync(organizationId);

        var allResourceAttributeAssignments = await resourceAttributeAssignmentRepository.GetAllAsync();
        var filteredResourceAttributeAssignments = allResourceAttributeAssignments
            .Where(raa => raa.OrganizationId == organizationId)
            .ToList();

        logger.LogDebug("Retrieved {Count} resource attribute assignments for organization. OrganizationId: {OrganizationId}", filteredResourceAttributeAssignments.Count, organizationId);
        return filteredResourceAttributeAssignments;
    }

    public async Task UpdateResourceAttributeAssignmentAsync(Guid resourceId, Guid resourceAttributeId, Guid organizationId)
    {
        logger.LogInformation("Updating resource attribute assignment. OrganizationId: {OrganizationId}, ResourceId: {ResourceId}, ResourceAttributeId: {ResourceAttributeId}",
            organizationId, resourceId, resourceAttributeId);

        await validationService.ValidationOrganizationAsync(organizationId);
        var resourceAttributeAssignment = await validationService.ValidateAndGetResourceAttributeAssignmentAsync(organizationId, resourceId, resourceAttributeId);

        resourceAttributeAssignment.ResourceId = resourceId;
        resourceAttributeAssignment.ResourceAttributeId = resourceAttributeId;
        await resourceAttributeAssignmentRepository.UpdateAsync(resourceAttributeAssignment);

        logger.LogInformation("Resource attribute assignment updated successfully. ResourceId: {ResourceId}, ResourceAttributeId: {ResourceAttributeId}, OrganizationId: {OrganizationId}", resourceId, resourceAttributeId, organizationId);
    }

    public async Task DeleteResourceAttributeAssignmentAsync(Guid resourceId, Guid resourceAttributeId, Guid organizationId)
    {
        logger.LogInformation("Deleting resource attribute assignment. OrganizationId: {OrganizationId}, ResourceId: {ResourceId}, ResourceAttributeId: {ResourceAttributeId}",
            organizationId, resourceId, resourceAttributeId);

        await validationService.ValidationOrganizationAsync(organizationId);
        var resourceAttributeAssignment = await validationService.ValidateAndGetResourceAttributeAssignmentAsync(organizationId, resourceId, resourceAttributeId);

        await resourceAttributeAssignmentRepository.DeleteAsync(resourceAttributeAssignment);

        logger.LogInformation("Resource attribute assignment deleted successfully. ResourceId: {ResourceId}, ResourceAttributeId: {ResourceAttributeId}, OrganizationId: {OrganizationId}", resourceId, resourceAttributeId, organizationId);
    }
}