namespace Chronos.Engine.Constraints;

public interface IConstraintProcessor
{
    /// <summary>
    /// Returns the set of Slot IDs that MUST NOT be used for the given Activity
    /// based on all ActivityConstraints and UserConstraints (if userId and schedulingPeriodId are provided)
    /// </summary>
    Task<HashSet<Guid>> GetExcludedSlotIdsAsync(
        Guid activityId, 
        Guid organizationId,
        Guid? userId = null,
        Guid? schedulingPeriodId = null
    );
}
