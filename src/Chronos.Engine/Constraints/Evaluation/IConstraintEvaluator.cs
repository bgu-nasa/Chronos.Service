using Chronos.Domain.Constraints;
using Chronos.Domain.Resources;
using Chronos.Domain.Schedule;

namespace Chronos.Engine.Constraints.Evaluation;

/// <summary>
/// Evaluates whether an Activity can be assigned to a specific (Slot, Resource) pair
/// based on ActivityConstraint records
/// </summary>
public interface IConstraintEvaluator
{
    /// <summary>
    /// Determines if an Activity can be assigned to the given Slot and Resource
    /// </summary>
    /// <param name="activity">The activity to assign</param>
    /// <param name="slot">The time slot</param>
    /// <param name="resource">The resource (e.g., room, equipment)</param>
    /// <returns>True if assignment is valid (no hard constraint violations), false otherwise</returns>
    Task<bool> CanAssignAsync(Activity activity, Slot slot, Resource resource, int? weekNum = null);

    /// <summary>
    /// Gets all constraint violations for the given Activity, Slot, and Resource combination
    /// </summary>
    /// <param name="activity">The activity to assign</param>
    /// <param name="slot">The time slot</param>
    /// <param name="resource">The resource (e.g., room, equipment)</param>
    /// <returns>Collection of all constraint violations (both hard and soft)</returns>
    Task<IEnumerable<ConstraintViolation>> GetViolationsAsync(
        Activity activity,
        Slot slot,
        Resource resource,
        int? weekNum = null
    );
}
