using Chronos.Data.Repositories.Resources;
using Chronos.Domain.Resources;
using Chronos.MainApi.Shared.ExternalMangement;
using Chronos.Shared.Exceptions;

namespace Chronos.MainApi.Resources.Services;

public class ResourceValidationService(
    IManagementExternalService managementExternalService,
    ISubjectRepository subjectRepository,
    IActivityRepository activityRepository,
    IResourceRepository resourceRepository,
    IResourceTypeRepository resourceTypeRepository,
    IResourceAttributeRepository resourceAttributeRepository,
    IResourceAttributeAssignmentRepository resourceAttributeAssignmentRepository,
    ILogger<ResourceValidationService> logger)
{
    public async Task ValidationOrganizationAsync(Guid organizationId)
    {
        await managementExternalService.ValidateOrganizationAsync(organizationId);
    }

    public async Task<Subject> ValidateAndGetSubjectAsync(Guid organizationId, Guid subjectId)
    {
        var subject = await subjectRepository.GetByIdAsync(subjectId);

        if (subject == null || subject.OrganizationId != organizationId)
        {
            logger.LogWarning("Subject not found or does not belong to organization. SubjectId: {SubjectId}, OrganizationId: {OrganizationId}", subjectId, organizationId);
            throw new NotFoundException("Subject not found");
        }

        return subject;
    }

    public async Task<Activity> ValidateAndGetActivityAsync(Guid organizationId, Guid activityId)
    {
        var activity = await activityRepository.GetByIdAsync(activityId);

        if (activity == null || activity.OrganizationId != organizationId)
        {
            logger.LogWarning("Activity not found or does not belong to organization. ActivityId: {ActivityId}, OrganizationId: {OrganizationId}", activityId, organizationId);
            throw new NotFoundException("Activity not found");
        }

        return activity;
    }
    
        public async Task<Resource> ValidateAndGetResourceAsync(Guid organizationId, Guid resourceId)
    {
        var resource = await resourceRepository.GetByIdAsync(resourceId);

        if (resource == null || resource.OrganizationId != organizationId)
        {
            logger.LogWarning("Resource not found or does not belong to organization. ResourceId: {ResourceId}, OrganizationId: {OrganizationId}", resourceId, organizationId);
            throw new NotFoundException("Resource not found");
        }

        return resource;
    }

    public async Task<ResourceType> ValidateAndGetResourceTypeAsync(Guid organizationId, Guid resourceTypeId)
    {
        var resourceType = await resourceTypeRepository.GetByIdAsync(resourceTypeId);

        if (resourceType == null || resourceType.OrganizationId != organizationId)
        {
            logger.LogWarning("Resource type not found or does not belong to organization. ResourceTypeId: {ResourceTypeId}, OrganizationId: {OrganizationId}", resourceTypeId, organizationId);
            throw new NotFoundException("Resource type not found");
        }

        return resourceType;
    }

    public async Task<ResourceAttribute> ValidateAndGetResourceAttributeAsync(Guid organizationId, Guid resourceAttributeId)
    {
        var resourceAttribute = await resourceAttributeRepository.GetByIdAsync(resourceAttributeId);

        if (resourceAttribute == null || resourceAttribute.OrganizationId != organizationId)
        {
            logger.LogWarning("Resource attribute not found or does not belong to organization. ResourceAttributeId: {ResourceAttributeId}, OrganizationId: {OrganizationId}", resourceAttributeId, organizationId);
            throw new NotFoundException("Resource attribute not found");
        }

        return resourceAttribute;
    }

    public async Task<ResourceAttributeAssignment> ValidateAndGetResourceAttributeAssignmentAsync(Guid organizationId, Guid resourceId, Guid resourceAttributeId)
    {
        var resourceAttributeAssignment = await resourceAttributeAssignmentRepository.GetByIdAsync(resourceId, resourceAttributeId);

        if (resourceAttributeAssignment == null || resourceAttributeAssignment.OrganizationId != organizationId)
        {
            logger.LogWarning("Resource attribute assignment not found or does not belong to organization. ResourceId: {ResourceId}, ResourceAttributeId: {ResourceAttributeId}, OrganizationId: {OrganizationId}", resourceId, resourceAttributeId, organizationId);
            throw new NotFoundException("Resource attribute assignment not found");
        }

        return resourceAttributeAssignment;
    }
}