using Chronos.Data.Repositories.Resources;
using Chronos.Domain.Constraints;
using Chronos.Domain.Resources;
using Chronos.Domain.Schedule;
using Microsoft.Extensions.DependencyInjection;

namespace Chronos.Engine.Constraints.Evaluation.Validators;

/// <summary>
/// Validates that a resource's type is compatible with the activity type
/// Constraint Key: "compatible_resource_types"
/// Value Format: "Lecture Hall,Seminar Room,Laboratory" (comma-separated resource type names)
/// Type: Hard constraint (error if violated)
/// </summary>
public class ActivityTypeCompatibilityValidator(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ActivityTypeCompatibilityValidator> logger
) : IConstraintValidator
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly ILogger<ActivityTypeCompatibilityValidator> _logger = logger;

    public string ConstraintKey => "compatible_resource_types";

    public async Task<ConstraintViolation?> ValidateAsync(
        ActivityConstraint constraint,
        Activity activity,
        Slot slot,
        Resource resource
    )
    {
        _logger.LogInformation(
            "Validating compatible_resource_types constraint for Activity {ActivityId}, Resource {ResourceId}",
            activity.Id,
            resource.Id
        );

        try
        {
            // Parse comma-separated resource type names
            var compatibleTypeNames = constraint
                .Value.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                )
                .Select(t => t.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!compatibleTypeNames.Any())
            {
                _logger.LogWarning(
                    "Empty compatible resource types constraint for Activity {ActivityId}",
                    activity.Id
                );

                return new ConstraintViolation
                {
                    ConstraintKey = ConstraintKey,
                    ConstraintValue = constraint.Value,
                    ViolationType = ViolationType.Hard,
                    Severity = ViolationSeverity.Error,
                    Message = "No compatible resource types specified",
                    Details = constraint.Value,
                };
            }

            // Load the resource's type information
            _logger.LogTrace(
                "Loading resource type {ResourceTypeId} for Resource {ResourceId}",
                resource.ResourceTypeId,
                resource.Id
            );
            
            // Create a scope to resolve scoped dependencies
            using var scope = _serviceScopeFactory.CreateScope();
            var resourceTypeRepository = scope.ServiceProvider.GetRequiredService<IResourceTypeRepository>();
            var resourceType = await resourceTypeRepository.GetByIdAsync(resource.ResourceTypeId);

            if (resourceType == null)
            {
                _logger.LogWarning(
                    "Resource type {ResourceTypeId} not found for Resource {ResourceId}",
                    resource.ResourceTypeId,
                    resource.Id
                );

                return new ConstraintViolation
                {
                    ConstraintKey = ConstraintKey,
                    ConstraintValue = constraint.Value,
                    ViolationType = ViolationType.Hard,
                    Severity = ViolationSeverity.Error,
                    Message =
                        $"Resource type information not found for resource '{resource.Identifier}'",
                    Details = $"ResourceTypeId: {resource.ResourceTypeId}",
                };
            }

            // Check if resource's type is in the compatible list
            if (!compatibleTypeNames.Contains(resourceType.Type))
            {
                return new ConstraintViolation
                {
                    ConstraintKey = ConstraintKey,
                    ConstraintValue = constraint.Value,
                    ViolationType = ViolationType.Hard,
                    Severity = ViolationSeverity.Error,
                    Message =
                        $"Resource type '{resourceType.Type}' is not compatible with activity type '{activity.ActivityType}'",
                    Details =
                        $"Compatible types: {constraint.Value}, Resource type: {resourceType.Type}",
                };
            }

            // No violation - resource type is compatible
            _logger.LogTrace(
                "Resource type '{ResourceType}' is compatible for Activity {ActivityId}",
                resourceType.Type,
                activity.Id
            );
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error validating compatible_resource_types constraint for Activity {ActivityId}",
                activity.Id
            );

            return new ConstraintViolation
            {
                ConstraintKey = ConstraintKey,
                ConstraintValue = constraint.Value,
                ViolationType = ViolationType.Hard,
                Severity = ViolationSeverity.Error,
                Message = "Constraint validation error",
                Details = ex.Message,
            };
        }
    }
}
