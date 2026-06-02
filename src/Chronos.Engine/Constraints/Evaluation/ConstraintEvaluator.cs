using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Constraints;
using Chronos.Domain.Resources;
using Chronos.Domain.Schedule;
using Chronos.Engine.Matching;
using Microsoft.Extensions.DependencyInjection;

namespace Chronos.Engine.Constraints.Evaluation;

/// <summary>
/// Main implementation of constraint evaluation for Activity-Slot-Resource compatibility
/// </summary>
public class ConstraintEvaluator : IConstraintEvaluator
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ConstraintEvaluator> _logger;

    public ConstraintEvaluator(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ConstraintEvaluator> logger
    )
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> CanAssignAsync(
        Activity activity,
        Slot slot,
        Resource resource,
        int? weekNum = null,
        DateTime? schedulingPeriodFrom = null
    )
    {
        var violations = await GetViolationsAsync(activity, slot, resource, weekNum, schedulingPeriodFrom);

        // Assignment is valid only if there are no hard constraint violations
        var hasHardViolations = violations.Any(v => v.ViolationType == ViolationType.Hard);

        return !hasHardViolations;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ConstraintViolation>> GetViolationsAsync(
        Activity activity,
        Slot slot,
        Resource resource,
        int? weekNum = null,
        DateTime? schedulingPeriodFrom = null
    )
    {
        // Create a scope to resolve scoped dependencies (repository and validators)
        using var scope = _serviceScopeFactory.CreateScope();
        var constraintRepository = scope.ServiceProvider.GetRequiredService<IActivityConstraintRepository>();
        var validators = scope.ServiceProvider.GetServices<IConstraintValidator>();

        var violations = new List<ConstraintViolation>();

        // Load all constraints for this activity
        var constraints = await constraintRepository.GetByActivityIdAsync(activity.Id);
        if (weekNum.HasValue)
        {
            constraints = constraints
                .Where(constraint =>
                    PeriodWeekCalculator.ConstraintWeekApplies(
                        constraint.WeekNum,
                        weekNum.Value,
                        schedulingPeriodFrom
                    )
                )
                .ToList();
        }

        // Always enforce ExpectedStudents vs capacity when no required_capacity row exists
        // (RequiredCapacityValidator also runs when that row is present).
        if (!constraints.Any(c => c.Key == "required_capacity"))
        {
            violations.AddRange(EvaluateEnrollmentCapacity(activity, resource));
        }

        _logger.LogInformation(
            "Evaluating {ConstraintCount} constraints for Activity {ActivityId} with Slot {SlotId} and Resource {ResourceId}",
            constraints.Count,
            activity.Id,
            slot.Id,
            resource.Id
        );

        foreach (var constraint in constraints)
        {
            // Find validator for this constraint type
            var validator = validators.FirstOrDefault(v => v.ConstraintKey == constraint.Key);

            if (validator == null)
            {
                _logger.LogWarning(
                    "No validator found for constraint key '{ConstraintKey}'. Skipping.",
                    constraint.Key
                );
                continue;
            }

            // Validate the constraint
            var violation = await validator.ValidateAsync(constraint, activity, slot, resource);

            if (violation != null)
            {
                violations.Add(violation);

                _logger.LogInformation(
                    "Constraint violation detected: {ConstraintKey}={ConstraintValue}, Severity={Severity}, Message={Message}",
                    violation.ConstraintKey,
                    violation.ConstraintValue,
                    violation.Severity,
                    violation.Message
                );
            }
        }

        _logger.LogInformation(
            "Evaluation complete: {ViolationCount} violations found ({HardCount} hard, {SoftCount} soft)",
            violations.Count,
            violations.Count(v => v.ViolationType == ViolationType.Hard),
            violations.Count(v => v.ViolationType == ViolationType.Soft)
        );

        return violations;
    }

    /// <summary>
    /// Always enforce ExpectedStudents vs room capacity, independent of whether a
    /// <c>required_capacity</c> constraint row exists.
    /// </summary>
    private static IEnumerable<ConstraintViolation> EvaluateEnrollmentCapacity(
        Activity activity,
        Resource resource
    )
    {
        if (!activity.ExpectedStudents.HasValue)
        {
            yield break;
        }

        var expected = activity.ExpectedStudents.Value;

        if (!resource.Capacity.HasValue)
        {
            yield return new ConstraintViolation
            {
                ConstraintKey = "enrollment_capacity",
                ConstraintValue = expected.ToString(),
                ViolationType = ViolationType.Hard,
                Severity = ViolationSeverity.Error,
                Message =
                    $"Resource '{resource.Identifier}' does not have capacity information for {expected} expected students",
                Details =
                    $"Expected students: {expected}, Resource capacity: null",
            };
            yield break;
        }

        var capacity = resource.Capacity.Value;
        if (capacity < expected)
        {
            yield return new ConstraintViolation
            {
                ConstraintKey = "enrollment_capacity",
                ConstraintValue = expected.ToString(),
                ViolationType = ViolationType.Hard,
                Severity = ViolationSeverity.Error,
                Message =
                    $"Resource capacity ({capacity}) is insufficient for expected students ({expected})",
                Details =
                    $"Expected students: {expected}, Resource capacity: {capacity}, Resource: {resource.Identifier}",
            };
        }
    }
}
