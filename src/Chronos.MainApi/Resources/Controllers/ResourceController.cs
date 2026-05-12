using Chronos.MainApi.Resources.Contracts;
using Chronos.MainApi.Resources.Extensions;
using Chronos.MainApi.Resources.Services;
using Chronos.MainApi.Shared.Controllers.Utils;
using Chronos.Shared.Exceptions;
using Chronos.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chronos.MainApi.Resources.Controllers;

[ApiController]
[Route("api/resources/[controller]")]
public class ResourceController(
    ILogger<ResourceController> logger,
    IResourceService resourceService,
    IResourceTypeService resourceTypeService,
    IResourceAttributeService resourceAttributeService,
    IResourceAttributeAssignmentService resourceAttributeAssignmentService
    ) : ControllerBase
{
    [Authorize(Policy = "OrgRole:ResourceManager")]
    [HttpPost]
    public async Task<IActionResult> CreateResourceAsync([FromBody] CreateResourceRequest request)
    {
        logger.LogInformation("Create resource endpoint was called.");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        
        var resource = await resourceService.CreateResourceAsync(
            request.OrganizationId,
            request.ResourceTypeId,
            request.Location,
            request.Identifier,
            request.Capacity);
        
        var response = resource.ToResourceResponse();
        
        return CreatedAtAction(nameof(GetResource), new { resourceId = resource.Id }, response);
    }
    
    [Authorize(Policy = "OrgRole:Viewer")]
    [HttpGet("{resourceId}")]
    public async Task<IActionResult> GetResource([FromRoute] Guid resourceId)
    {
        logger.LogInformation("Get resource endpoint was called for resource {ResourceId}", resourceId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        
        var resource = await resourceService.GetResourceAsync(organizationId, resourceId);
        if (resource == null)
            return NotFound();

        var resourceResponse = resource.ToResourceResponse();
        
        return Ok(resourceResponse);
    }
    
    [Authorize(Policy = "OrgRole:Viewer")]
    [HttpGet]
    public async Task<IActionResult> GetResourcesAsync()
    {
        logger.LogInformation("Get resources endpoint was called.");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        
        var resources = await resourceService.GetResourcesAsync(organizationId);

        var resourceResponses = resources.Select(r => r.ToResourceResponse()).ToList();
        
        return Ok(resourceResponses);
    }
    
    [Authorize(Policy = "OrgRole:ResourceManager")]
    [HttpPatch("{resourceId}")]
    public async Task<IActionResult> UpdateResourceAsync(Guid resourceId, [FromBody] UpdateResourceRequest request)
    {
        logger.LogInformation("Update resource endpoint was called for resource {ResourceId}", resourceId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        
        await resourceService.UpdateResourceAsync(
            organizationId,
            resourceId,
            request.ResourceTypeId,
            request.Location,
            request.Identifier,
            request.Capacity);

        return NoContent();
    }
    
    [Authorize(Policy = "OrgRole:ResourceManager")]
    [HttpDelete("{resourceId}")]
    public async Task<IActionResult> DeleteResourceAsync(Guid resourceId)
    {
        logger.LogInformation("Delete resource endpoint was called for resource {ResourceId}", resourceId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        
        await resourceService.DeleteResourceAsync(organizationId, resourceId);

        return NoContent();
    }
    
    [Authorize(Policy = "OrgRole:ResourceManager")]
    [HttpPost("types")]
    public async Task<IActionResult> CreateResourceTypeAsync([FromBody] CreateResourceTypeRequest request)
    {
        logger.LogInformation("Create resource type endpoint was called.");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        
        var resourceType = await resourceTypeService.CreateResourceTypeAsync(
            request.OrganizationId,
            request.Type);
        
        var response = resourceType.ToResourceTypeResponse();
        
        return CreatedAtAction(nameof(GetResourceType), new { resourceTypeId = resourceType.Id }, response);
    }
    
    [Authorize(Policy = "OrgRole:Viewer")]
    [HttpGet("types/{resourceTypeId}")]
    public async Task<IActionResult> GetResourceType(Guid resourceTypeId)
    {
        logger.LogInformation("Get resource type endpoint was called for resource type {ResourceTypeId}", resourceTypeId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        
        var resourceType = await resourceTypeService.GetResourceTypeAsync(organizationId, resourceTypeId);
        if (resourceType == null)
            return NotFound();

        var resourceTypeResponse = resourceType.ToResourceTypeResponse();
        
        return Ok(resourceTypeResponse);
    }
    
    [Authorize(Policy = "OrgRole:Viewer")]
    [HttpGet("types")]
    public async Task<IActionResult> GetResourceTypesAsync()
    {
        logger.LogInformation("Get resource types endpoint was called.");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        
        var resourceTypes = await resourceTypeService.GetResourceTypesAsync(organizationId);

        var resourceTypeResponses = resourceTypes.Select(rt => rt.ToResourceTypeResponse()).ToList();
        
        return Ok(resourceTypeResponses);
    }
    
    [Authorize(Policy = "OrgRole:ResourceManager")]
    [HttpPatch("types/{resourceTypeId}")]
    public async Task<IActionResult> UpdateResourceTypeAsync(Guid resourceTypeId, [FromBody] UpdateResourceTypeRequest request)
    {
        logger.LogInformation("Update resource type endpoint was called for resource type {ResourceTypeId}", resourceTypeId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        
        await resourceTypeService.UpdateResourceTypeAsync(
            organizationId,
            resourceTypeId,
            request.Type);

        return NoContent();
    }
    
    [Authorize(Policy = "OrgRole:ResourceManager")]
    [HttpDelete("types/{resourceTypeId}")]
    public async Task<IActionResult> DeleteResourceTypeAsync(Guid resourceTypeId)
    {
        logger.LogInformation("Delete resource type endpoint was called for resource type {ResourceTypeId}", resourceTypeId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        
        await resourceTypeService.DeleteResourceTypeAsync(organizationId, resourceTypeId);

        return NoContent();
    }
    
    [Authorize(Policy = "OrgRole:ResourceManager")]
    [HttpPost("attributes")]
    public async Task<IActionResult> CreateResourceAttributeAsync([FromBody] CreateResourceAttributeRequest request)
    {
        logger.LogInformation("Create resource attribute endpoint was called.");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        
        var resourceAttribute = await resourceAttributeService.CreateResourceAttributeAsync(
            request.OrganizationId,
            request.Title,
            request.Description);
        
        var response = resourceAttribute.ToResourceAttributeResponse();
        
        return CreatedAtAction(nameof(GetResourceAttribute), new { resourceAttributeId = resourceAttribute.Id }, response);
    }
    
    [Authorize(Policy = "OrgRole:Viewer")]
    [HttpGet("attributes/{resourceAttributeId}")]
    public async Task<IActionResult> GetResourceAttribute(Guid resourceAttributeId)
    {
        logger.LogInformation("Get resource attribute endpoint was called for resource attribute {ResourceAttributeId}", resourceAttributeId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        
        var resourceAttribute = await resourceAttributeService.GetResourceAttributeAsync(organizationId, resourceAttributeId);
        if (resourceAttribute == null)
            return NotFound();

        var resourceAttributeResponse = resourceAttribute.ToResourceAttributeResponse();
        
        return Ok(resourceAttributeResponse);
    }
    
    [Authorize(Policy = "OrgRole:Viewer")]
    [HttpGet("attributes")]
    public async Task<IActionResult> GetResourceAttributesAsync()
    {
        logger.LogInformation("Get resource attributes endpoint was called.");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        
        var resourceAttributes = await resourceAttributeService.GetResourceAttributesAsync(organizationId);

        var resourceAttributeResponses = resourceAttributes.Select(ra => ra.ToResourceAttributeResponse()).ToList();
        
        return Ok(resourceAttributeResponses);
    }
    
    [Authorize(Policy = "OrgRole:ResourceManager")]
    [HttpPatch("attributes/{resourceAttributeId}")]
    public async Task<IActionResult> UpdateResourceAttributeAsync(Guid resourceAttributeId, [FromBody] UpdateResourceAttributeRequest request)
    {
        logger.LogInformation("Update resource attribute endpoint was called for resource attribute {ResourceAttributeId}", resourceAttributeId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        
        await resourceAttributeService.UpdateResourceAttributeAsync(
            organizationId,
            resourceAttributeId,
            request.Title,
            request.Description);

        return NoContent();
    }
    
    [Authorize(Policy = "OrgRole:ResourceManager")]
    [HttpDelete("attributes/{resourceAttributeId}")]
    public async Task<IActionResult> DeleteResourceAttributeAsync(Guid resourceAttributeId)
    {
        logger.LogInformation("Delete resource attribute endpoint was called for resource attribute {ResourceAttributeId}", resourceAttributeId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        
        await resourceAttributeService.DeleteResourceAttributeAsync(organizationId, resourceAttributeId);

        return NoContent();
    }
    
    [Authorize(Policy = "OrgRole:ResourceManager")]
    [HttpPost("attribute-assignments")]
    public async Task<IActionResult> CreateResourceAttributeAssignmentAsync([FromBody] CreateResourceAttributeAssignmentRequest request)
    {
        logger.LogInformation("Create resource attribute assignment endpoint was called.");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        
        var resourceAttributeAssignment = await resourceAttributeAssignmentService.CreateResourceAttributeAssignmentAsync(
            request.ResourceId,
            request.ResourceAttributeId,
            request.OrganizationId);
        
        var response = resourceAttributeAssignment.ToResourceAttributeAssignmentResponse();
        
        return CreatedAtAction(nameof(GetResourceAttributeAssignment), new { resourceId = request.ResourceId, resourceAttributeId = resourceAttributeAssignment.ResourceAttributeId }, response);
    }
    
    [Authorize(Policy = "OrgRole:Viewer")]
    [HttpGet("attribute-assignments/{resourceId}/{resourceAttributeId}")]
    public async Task<IActionResult> GetResourceAttributeAssignment(Guid resourceId, Guid resourceAttributeId)
    {
        logger.LogInformation("Get resource attribute assignment endpoint was called for resource {ResourceId} and attribute {ResourceAttributeId}", resourceId, resourceAttributeId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        
        var resourceAttributeAssignment = await resourceAttributeAssignmentService.GetResourceAttributeAssignmentAsync(resourceId, resourceAttributeId, organizationId);
        if (resourceAttributeAssignment == null)
            return NotFound();

        var resourceAttributeAssignmentResponse = resourceAttributeAssignment.ToResourceAttributeAssignmentResponse();
        
        return Ok(resourceAttributeAssignmentResponse);
    }
    
    [Authorize(Policy = "OrgRole:Viewer")]
    [HttpGet("attribute-assignments")]
    public async Task<IActionResult> GetAllResourceAttributeAssignmentsAsync()
    {
        logger.LogInformation("Get resource attribute assignments endpoint was called.");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        
        var resourceAttributeAssignments = await resourceAttributeAssignmentService.GetAllResourceAttributeAssignmentsAsync(organizationId);

        var resourceAttributeAssignmentResponses = resourceAttributeAssignments
            .Select(raa => raa.ToResourceAttributeAssignmentResponse())
            .ToList();
        
        return Ok(resourceAttributeAssignmentResponses);
    }
    
    [Authorize(Policy = "OrgRole:ResourceManager")]
    [HttpPatch("attribute-assignments/{resourceId}/{resourceAttributeId}")]
    public async Task<IActionResult> UpdateResourceAttributeAssignmentAsync(Guid resourceId, Guid resourceAttributeId, [FromBody] UpdateResourceAttributeAssignmentRequest request)
    {
        logger.LogInformation("Update resource attribute assignment endpoint was called for resource {ResourceId} and attribute {ResourceAttributeId}", resourceId, resourceAttributeId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        
        await resourceAttributeAssignmentService.UpdateResourceAttributeAssignmentAsync(
            resourceId,
            resourceAttributeId,
            organizationId);

        return NoContent();
    }
    
    [Authorize(Policy = "OrgRole:ResourceManager")]
    [HttpDelete("attribute-assignments/{resourceId}/{resourceAttributeId}")]
    public async Task<IActionResult> DeleteResourceAttributeAssignmentAsync(Guid resourceId, Guid resourceAttributeId)
    {
        logger.LogInformation("Delete resource attribute assignment endpoint was called for resource {ResourceId} and attribute {ResourceAttributeId}", resourceId, resourceAttributeId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        
        await resourceAttributeAssignmentService.DeleteResourceAttributeAssignmentAsync(resourceId, resourceAttributeId, organizationId);

        return NoContent();
    }
    
}