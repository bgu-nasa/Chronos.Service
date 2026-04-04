using Chronos.Data.Repositories.Resources;
using Chronos.Domain.Resources;

namespace Chronos.MainApi.Resources.Services;

public class ResourceService(
    IResourceRepository resourceRepository,
    ResourceValidationService validationService,
    ILogger<ResourceService> logger) : IResourceService
{
    public async Task<Resource> CreateResourceAsync(Guid id, Guid organizationId, Guid resourceTypeId, string location, string identifier,
        int? capacity)
    {
        logger.LogInformation("Creating resource. OrganizationId: {OrganizationId}, ResourceTypeId: {ResourceTypeId}, Location: {Location}, Identifier: {Identifier}, Capacity: {Capacity}",
            organizationId, resourceTypeId, location, identifier, capacity);

        await validationService.ValidationOrganizationAsync(organizationId);

        var resource = new Resource
        {
            OrganizationId = organizationId,
            ResourceTypeId = resourceTypeId,
            Location = location,
            Identifier = identifier,
            Capacity = capacity
        };

        await resourceRepository.AddAsync(resource);

        logger.LogInformation("Resource created successfully. ResourceId: {ResourceId}, OrganizationId: {OrganizationId}", resource.Id, organizationId);
        return resource;
    }

    public async Task<Resource> GetResourceAsync(Guid organizationId, Guid resourceId)
    {
        logger.LogDebug("Retrieving resource. OrganizationId: {OrganizationId}, ResourceId: {ResourceId}", organizationId, resourceId);

        await validationService.ValidationOrganizationAsync(organizationId);
        var resource = await validationService.ValidateAndGetResourceAsync(organizationId, resourceId);
        return resource;
    } 

    public async Task<List<Resource>> GetResourcesAsync(Guid organizationId)
    {
        logger.LogDebug("Retrieving all resources for organization. OrganizationId: {OrganizationId}", organizationId);

        await validationService.ValidationOrganizationAsync(organizationId);

        var allResources = await resourceRepository.GetAllAsync();
        var filteredResources = allResources
            .Where(r => r.OrganizationId == organizationId)
            .ToList();

        logger.LogDebug("Retrieved {Count} resources for organization. OrganizationId: {OrganizationId}", filteredResources.Count, organizationId);
        return filteredResources;
    }

    public async Task UpdateResourceAsync(Guid organizationId, Guid resourceId, Guid resourceTypeId, string location, string identifier,
        int? capacity)
    {
        logger.LogInformation("Updating resource. OrganizationId: {OrganizationId}, ResourceId: {ResourceId}, ResourceTypeId: {ResourceTypeId}, Location: {Location}, Identifier: {Identifier}, Capacity: {Capacity}",
            organizationId, resourceId, resourceTypeId, location, identifier, capacity);

        await validationService.ValidationOrganizationAsync(organizationId);
        var resource = await validationService.ValidateAndGetResourceAsync(organizationId, resourceId);

        resource.ResourceTypeId = resourceTypeId;
        resource.Location = location;
        resource.Identifier = identifier;
        resource.Capacity = capacity;
        await resourceRepository.UpdateAsync(resource);

        logger.LogInformation("Resource updated successfully. ResourceId: {ResourceId}, OrganizationId: {OrganizationId}", resource.Id, organizationId);
    }

    public async Task DeleteResourceAsync(Guid organizationId, Guid resourceId)
    {
        logger.LogInformation("Deleting resource. OrganizationId: {OrganizationId}, ResourceId: {ResourceId}", organizationId, resourceId);

        await validationService.ValidationOrganizationAsync(organizationId);
        var resource = await validationService.ValidateAndGetResourceAsync(organizationId, resourceId);
        await resourceRepository.DeleteAsync(resource);

        logger.LogInformation("Resource deleted successfully. ResourceId: {ResourceId}, OrganizationId: {OrganizationId}", resource.Id, organizationId);
    }

    public async Task DeleteResourcesByResourceTypeIdAsync(Guid organizationId, Guid resourceTypeId)
    {
        logger.LogInformation("Deleting resources by resource type. OrganizationId: {OrganizationId}, ResourceTypeId: {ResourceTypeId}", organizationId, resourceTypeId);

        await validationService.ValidationOrganizationAsync(organizationId);
        await resourceRepository.DeleteByResourceTypeIdAsync(resourceTypeId);

        logger.LogInformation("Resources deleted successfully by resource type. ResourceTypeId: {ResourceTypeId}, OrganizationId: {OrganizationId}", resourceTypeId, organizationId);
    }
}