using System.Diagnostics;
using System.Text.Json;
using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Schedule;
using Chronos.MainApi.Shared.ExternalMangement;
using Chronos.Shared.Exceptions;

namespace Chronos.MainApi.Schedule.Services;

public class ActivityConstraintService(
    IActivityConstraintRepository activityConstraintRepository,
    ILogger<ActivityConstraintService> logger,
    IManagementExternalService validationService
) : IActivityConstraintService
{


    public async Task<Guid> CreateActivityConstraintAsync(Guid organizationId, Guid activityId, string key, string value)
    {
        
        await validationService.ValidateOrganizationAsync(organizationId);
        ValidateConstraintValue(key, value);
        var constraint = new ActivityConstraint
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ActivityId = activityId,
            Key = key,
            Value = value
        };
        await activityConstraintRepository.AddAsync(constraint);
        logger.LogInformation("Created ActivityConstraint {ActivityConstraintId} for Organization {OrganizationId}", constraint.Id, organizationId);
        return constraint.Id;
    }
    
    public async Task<ActivityConstraint> GetActivityConstraintByIdAsync(Guid organizationId, Guid activityConstraintId)
    {
        logger.LogInformation("Retrieving ActivityConstraint {ActivityConstraintId} for Organization {OrganizationId}", activityConstraintId, organizationId);
        var activityConstraint = await ValidateAndGetActivityConstraintAsync(organizationId , activityConstraintId);
        logger.LogInformation("Retrieved ActivityConstraint {ActivityConstraintId} for Organization {OrganizationId}", activityConstraintId, organizationId);
        return activityConstraint;
    }
    
    public async Task<List<ActivityConstraint>> GetAllActivityConstraintsAsync(Guid organizationId)
    {
        logger.LogInformation("Retrieving all ActivityConstraints for Organization {OrganizationId}", organizationId);
        await validationService.ValidateOrganizationAsync(organizationId);
        var constraints = await activityConstraintRepository.GetAllAsync();
        var orgConstraints = constraints.Where(ac => ac.OrganizationId == organizationId).ToList();
        logger.LogInformation("Retrieved {Count} ActivityConstraints for Organization {OrganizationId}", orgConstraints.Count, organizationId);
        return orgConstraints;
    }
    
    public async Task<List<ActivityConstraint>> GetByActivityIdAsync(Guid organizationId, Guid activityId)
    {
        logger.LogInformation("Retrieving ActivityConstraints for Activity {ActivityId} in Organization {OrganizationId}", activityId, organizationId);
        await validationService.ValidateOrganizationAsync(organizationId);
        var constraints = await activityConstraintRepository.GetByActivityIdAsync(activityId);
        var orgConstraints = constraints.Where(ac => ac.OrganizationId == organizationId).ToList();
        logger.LogInformation("Retrieved {Count} ActivityConstraints for Activity {ActivityId} in Organization {OrganizationId}", orgConstraints.Count, activityId, organizationId);
        return orgConstraints;
    }

    public async Task<ActivityConstraint> UpdateActivityConstraintAsync(Guid organizationId, Guid activityConstraintId,
        string key, string value)
    {
        logger.LogInformation("Updating ActivityConstraint {ActivityConstraintId} for Organization {OrganizationId}", activityConstraintId, organizationId);
        ValidateConstraintValue(key, value);
        var constraint = await ValidateAndGetActivityConstraintAsync(organizationId, activityConstraintId);
        constraint.Key = key;
        constraint.Value = value;
        await activityConstraintRepository.UpdateAsync(constraint);
        logger.LogInformation("Updated ActivityConstraint {ActivityConstraintId} for Organization {OrganizationId}", activityConstraintId, organizationId);
        return constraint;
    }
    
    public async Task DeleteActivityConstraintAsync(Guid organizationId, Guid activityConstraintId)
    {
        logger.LogInformation("Deleting ActivityConstraint {ActivityConstraintId} for Organization {OrganizationId}", activityConstraintId, organizationId);
        var constraint = await ValidateAndGetActivityConstraintAsync(organizationId, activityConstraintId);
        await activityConstraintRepository.DeleteAsync(constraint);
        logger.LogInformation("Deleted ActivityConstraint {ActivityConstraintId} for Organization {OrganizationId}", activityConstraintId, organizationId);
    }

    private async Task<ActivityConstraint> ValidateAndGetActivityConstraintAsync(Guid organizationId, Guid activityConstraintId)
    {
        var activityConstraint = await activityConstraintRepository.GetByIdAsync(activityConstraintId);
        if (activityConstraint == null || activityConstraint.OrganizationId != organizationId)
        {
            logger.LogInformation(
                "ActivityConstraint not found or does not belong to organization. ActivityConstraintId: {ActivityConstraintId}, OrganizationId: {OrganizationId}",
                activityConstraintId, organizationId);
            throw new NotFoundException("Activity constraint not found");
        }
        return activityConstraint;
    }
    private void ValidateConstraintValue(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or empty.", nameof(value));
        }

        // Only validate JSON format for constraint types that require JSON
        // JSON-based constraints: required_capacity, time_range
        // String-based constraints: compatible_resource_types, location_preference, preferred_weekdays, forbidden_timerange
        var jsonBasedConstraints = new[] { "required_capacity", "time_range" };
        
        if (jsonBasedConstraints.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                JsonDocument.Parse(value);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException($"Value must be valid JSON: {ex.Message}", nameof(value), ex);
            }
        }
        // For non-JSON constraints, just ensure it's not empty (format validation happens in validators)
    }
}