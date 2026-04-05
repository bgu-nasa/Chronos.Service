using Chronos.Data.Context;
using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Resources;
using Chronos.Domain.Schedule;
using Chronos.Domain.Schedule.Messages;
using Chronos.Engine.Constraints;
using Chronos.Engine.Constraints.Evaluation;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Engine.Matching;

/// <summary>
/// Online mode matching for mid-semester constraint changes
/// Uses greedy re-matching with minimal disruption
/// </summary>
public class OnlineMatchingStrategy(
    IAssignmentRepository assignmentRepository,
    IConstraintProcessor constraintProcessor,
    IConstraintEvaluator constraintEvaluator,
    PreferenceWeightedRanker ranker,
    AppDbContext dbContext,
    ILogger<OnlineMatchingStrategy> logger
) : IMatchingStrategy
{
    private readonly IAssignmentRepository _assignmentRepository = assignmentRepository;
    private readonly IConstraintProcessor _constraintProcessor = constraintProcessor;
    private readonly IConstraintEvaluator _constraintEvaluator = constraintEvaluator;
    private readonly PreferenceWeightedRanker _ranker = ranker;
    private readonly AppDbContext _dbContext = dbContext;
    private readonly ILogger<OnlineMatchingStrategy> _logger = logger;

    public SchedulingMode Mode => SchedulingMode.Online;

    public async Task<SchedulingResult> ExecuteAsync(
        object request,
        CancellationToken cancellationToken
    )
    {
        if (request is not HandleConstraintChangeRequest constraintRequest)
        {
            throw new ArgumentException(
                $"Expected HandleConstraintChangeRequest, got {request.GetType().Name}"
            );
        }

        _logger.LogInformation(
            "Starting Online Matching. ConstraintId: {ConstraintId}, Scope: {Scope}, Operation: {Operation}, PeriodId: {PeriodId}",
            constraintRequest.ActivityConstraintId,
            constraintRequest.Scope,
            constraintRequest.Operation,
            constraintRequest.SchedulingPeriodId
        );

        try
        {
            return constraintRequest.Scope switch
            {
                ConstraintScope.User => await HandleUserConstraintChangeAsync(
                    constraintRequest,
                    cancellationToken
                ),
                _ => await HandleActivityConstraintChangeAsync(constraintRequest, cancellationToken),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Online Matching");
            return new SchedulingResult(
                constraintRequest.ActivityConstraintId,
                false,
                0,
                0,
                new List<Guid>(),
                $"Algorithm failed: {ex.Message}"
            );
        }
    }

    private async Task<SchedulingResult> HandleActivityConstraintChangeAsync(
        HandleConstraintChangeRequest request,
        CancellationToken cancellationToken
    )
    {
        var activityId = request.ActivityId;

        if (!activityId.HasValue || activityId.Value == Guid.Empty)
        {
            var constraint = await _dbContext
                .ActivityConstraints.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == request.ActivityConstraintId, cancellationToken);
            activityId = constraint?.ActivityId;
        }

        if (!activityId.HasValue || activityId.Value == Guid.Empty)
        {
            return new SchedulingResult(
                request.ActivityConstraintId,
                false,
                0,
                0,
                new List<Guid>(),
                "Activity id could not be resolved from constraint event"
            );
        }

        var activity = await _dbContext
            .Activities.IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                a => a.Id == activityId.Value && a.OrganizationId == request.OrganizationId,
                cancellationToken
            );
        if (activity == null)
        {
            return new SchedulingResult(
                request.ActivityConstraintId,
                false,
                0,
                0,
                new List<Guid>(),
                "Activity not found"
            );
        }

        var assignment = (await _assignmentRepository.GetByActivityIdAsync(activity.Id)).FirstOrDefault();
        if (assignment == null)
        {
            return new SchedulingResult(
                request.ActivityConstraintId,
                true,
                0,
                0,
                new List<Guid>(),
                "Activity not currently assigned"
            );
        }

        var schedulingPeriodId = await ResolveSchedulingPeriodIdAsync(request.SchedulingPeriodId, assignment);
        if (schedulingPeriodId == Guid.Empty)
        {
            return new SchedulingResult(
                request.ActivityConstraintId,
                false,
                0,
                0,
                new List<Guid> { activity.Id },
                "Could not resolve scheduling period for assignment"
            );
        }

        var excludedSlots = await GetExcludedSlotsAsync(
            activity,
            request.OrganizationId,
            schedulingPeriodId
        );

        var isValid = await IsAssignmentValidAsync(activity, assignment, excludedSlots);
        if (isValid)
        {
            return new SchedulingResult(
                request.ActivityConstraintId,
                true,
                0,
                0,
                new List<Guid>(),
                "Current assignment remains valid"
            );
        }

        return await TryRescheduleAssignmentAsync(
            request.ActivityConstraintId,
            activity,
            assignment,
            excludedSlots,
            request.OrganizationId,
            schedulingPeriodId,
            cancellationToken
        );
    }

    private async Task<SchedulingResult> HandleUserConstraintChangeAsync(
        HandleConstraintChangeRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!request.UserId.HasValue || request.UserId.Value == Guid.Empty)
        {
            return new SchedulingResult(
                request.ActivityConstraintId,
                false,
                0,
                0,
                new List<Guid>(),
                "User-scope event missing user id"
            );
        }

        if (request.SchedulingPeriodId == Guid.Empty)
        {
            return new SchedulingResult(
                request.ActivityConstraintId,
                false,
                0,
                0,
                new List<Guid>(),
                "User-scope event missing scheduling period id"
            );
        }

        var assignments = await _assignmentRepository.GetBySchedulingPeriodIdAsync(
            request.SchedulingPeriodId
        );

        if (!assignments.Any())
        {
            return new SchedulingResult(
                request.ActivityConstraintId,
                true,
                0,
                0,
                new List<Guid>(),
                "No assignments found in scheduling period"
            );
        }

        var activityIds = assignments.Select(a => a.ActivityId).Distinct().ToList();
        var activities = await _dbContext
            .Activities.IgnoreQueryFilters()
            .Where(a => a.OrganizationId == request.OrganizationId)
            .Where(a => activityIds.Contains(a.Id))
            .Where(a => a.AssignedUserId == request.UserId.Value)
            .ToListAsync(cancellationToken);

        var activitiesById = activities.ToDictionary(a => a.Id);
        var affectedAssignments = assignments
            .Where(a => activitiesById.ContainsKey(a.ActivityId))
            .ToList();

        if (!affectedAssignments.Any())
        {
            return new SchedulingResult(
                request.ActivityConstraintId,
                true,
                0,
                0,
                new List<Guid>(),
                "No assigned activities were affected by this user constraint"
            );
        }

        var modifiedCount = 0;
        var unresolvedActivityIds = new List<Guid>();

        foreach (var assignment in affectedAssignments)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var activity = activitiesById[assignment.ActivityId];
            var excludedSlots = await GetExcludedSlotsAsync(
                activity,
                request.OrganizationId,
                request.SchedulingPeriodId
            );

            var isValid = await IsAssignmentValidAsync(activity, assignment, excludedSlots);
            if (isValid)
            {
                continue;
            }

            var rematchResult = await TryRescheduleAssignmentAsync(
                request.ActivityConstraintId,
                activity,
                assignment,
                excludedSlots,
                request.OrganizationId,
                request.SchedulingPeriodId,
                cancellationToken
            );

            modifiedCount += rematchResult.AssignmentsModified;
            if (!rematchResult.Success)
            {
                unresolvedActivityIds.Add(activity.Id);
            }
        }

        var success = !unresolvedActivityIds.Any();
        var reason = success
            ? "User-constraint online validation completed"
            : "Some activities could not be rescheduled according to the new constraint. Consider assigning a substitute lecturer or cancelling the affected activity.";

        return new SchedulingResult(
            request.ActivityConstraintId,
            success,
            0,
            modifiedCount,
            unresolvedActivityIds,
            reason
        );
    }

    private async Task<SchedulingResult> TryRescheduleAssignmentAsync(
        Guid requestId,
        Activity activity,
        Assignment currentAssignment,
        HashSet<Guid> excludedSlots,
        Guid organizationId,
        Guid schedulingPeriodId,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation("Re-matching Activity {ActivityId}", activity.Id);

        var allAssignments = await _assignmentRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId);
        var occupiedPairs = allAssignments
            .Where(a => a.Id != currentAssignment.Id)
            .Select(a => (a.SlotId, a.ResourceId))
            .ToHashSet();

        var slots = await _dbContext
            .Slots.IgnoreQueryFilters()
            .Where(s => s.SchedulingPeriodId == schedulingPeriodId)
            .Where(s => s.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);
        var resources = await _dbContext
            .Resources.IgnoreQueryFilters()
            .Where(r => r.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

        // Stage A: try same slot with a different resource first.
        var sameSlotCandidates = await BuildCandidatesAsync(
            activity,
            slots.Where(s => s.Id == currentAssignment.SlotId && !excludedSlots.Contains(s.Id)).ToList(),
            resources,
            occupiedPairs,
            cancellationToken
        );

        if (sameSlotCandidates.Any())
        {
            var sameSlotSelection = await SelectCandidateAsync(
                sameSlotCandidates,
                activity,
                organizationId,
                schedulingPeriodId
            );

            currentAssignment.SlotId = sameSlotSelection.SlotId;
            currentAssignment.ResourceId = sameSlotSelection.ResourceId;
            await _assignmentRepository.UpdateAsync(currentAssignment);

            return new SchedulingResult(
                requestId,
                true,
                0,
                1,
                new List<Guid>(),
                "Activity successfully re-matched within the same timeslot"
            );
        }

        // Stage B: fallback to another slot and resource.
        var fallbackCandidates = await BuildCandidatesAsync(
            activity,
            slots
                .Where(s => s.Id != currentAssignment.SlotId)
                .Where(s => !excludedSlots.Contains(s.Id))
                .ToList(),
            resources,
            occupiedPairs,
            cancellationToken
        );

        if (!fallbackCandidates.Any())
        {
            _logger.LogError(
                "No available replacement found for Activity {ActivityId}. Keeping current assignment and reporting unresolved change.",
                activity.Id
            );

            return new SchedulingResult(
                requestId,
                false,
                0,
                0,
                new List<Guid> { activity.Id },
                "Activity could not be rescheduled according to the new constraint. Keep current assignment for now and consider assigning a substitute lecturer or cancelling the activity."
            );
        }

        var fallbackSelection = await SelectCandidateAsync(
            fallbackCandidates,
            activity,
            organizationId,
            schedulingPeriodId
        );

        currentAssignment.SlotId = fallbackSelection.SlotId;
        currentAssignment.ResourceId = fallbackSelection.ResourceId;
        await _assignmentRepository.UpdateAsync(currentAssignment);

        _logger.LogInformation(
            "Successfully re-matched Activity {ActivityId} using fallback stage - New assignment: Slot {SlotId}, Resource {ResourceId}",
            activity.Id,
            currentAssignment.SlotId,
            currentAssignment.ResourceId
        );

        return new SchedulingResult(
            requestId,
            true,
            0,
            1,
            new List<Guid>(),
            "Activity successfully re-matched"
        );
    }

    private async Task<List<SlotResourcePair>> BuildCandidatesAsync(
        Activity activity,
        List<Slot> slots,
        List<Resource> resources,
        HashSet<(Guid SlotId, Guid ResourceId)> occupiedPairs,
        CancellationToken cancellationToken
    )
    {
        var candidates = new List<SlotResourcePair>();

        foreach (var slot in slots)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            foreach (var resource in resources)
            {
                if (occupiedPairs.Contains((slot.Id, resource.Id)))
                {
                    continue;
                }

                var canAssign = await _constraintEvaluator.CanAssignAsync(activity, slot, resource);
                if (!canAssign)
                {
                    continue;
                }

                candidates.Add(new SlotResourcePair(slot, resource));
            }
        }

        return candidates;
    }

    private async Task<SlotResourcePair> SelectCandidateAsync(
        List<SlotResourcePair> candidates,
        Activity activity,
        Guid organizationId,
        Guid schedulingPeriodId
    )
    {
        var weights = new double[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
        {
            weights[i] = await _ranker.CalculateWeightAsync(
                candidates[i],
                activity.AssignedUserId,
                organizationId,
                schedulingPeriodId
            );
        }

        return _ranker.SelectRandomWeighted(candidates, weights);
    }

    private async Task<HashSet<Guid>> GetExcludedSlotsAsync(
        Activity activity,
        Guid organizationId,
        Guid schedulingPeriodId
    )
    {
        return await _constraintProcessor.GetExcludedSlotIdsAsync(
            activity.Id,
            organizationId,
            activity.AssignedUserId != Guid.Empty ? activity.AssignedUserId : null,
            schedulingPeriodId
        );
    }

    private async Task<bool> IsAssignmentValidAsync(
        Activity activity,
        Assignment assignment,
        HashSet<Guid> excludedSlots
    )
    {
        if (excludedSlots.Contains(assignment.SlotId))
        {
            return false;
        }

        var slot = await _dbContext
            .Slots.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == assignment.SlotId);
        var resource = await _dbContext
            .Resources.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == assignment.ResourceId);

        if (slot == null || resource == null)
        {
            return false;
        }

        return await _constraintEvaluator.CanAssignAsync(activity, slot, resource);
    }

    private async Task<Guid> ResolveSchedulingPeriodIdAsync(
        Guid requestedSchedulingPeriodId,
        Assignment assignment
    )
    {
        if (requestedSchedulingPeriodId != Guid.Empty)
        {
            return requestedSchedulingPeriodId;
        }

        var slot = await _dbContext
            .Slots.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == assignment.SlotId);
        return slot?.SchedulingPeriodId ?? Guid.Empty;
    }
}
