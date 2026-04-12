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

        var assignments = await GetOrderedAssignmentsByActivityAsync(activity.Id, cancellationToken);
        if (!assignments.Any())
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

        var schedulingPeriodId = await ResolveSchedulingPeriodIdAsync(
            request.SchedulingPeriodId,
            assignments[0]
        );
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

        var isValid = await IsAssignmentSetValidAsync(
            activity,
            assignments,
            excludedSlots,
            cancellationToken
        );
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
            assignments,
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
        var affectedActivityIds = assignments
            .Where(a => activitiesById.ContainsKey(a.ActivityId))
            .Select(a => a.ActivityId)
            .Distinct()
            .ToList();

        if (!affectedActivityIds.Any())
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

        foreach (var activityId in affectedActivityIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var activity = activitiesById[activityId];
            var currentAssignments = await GetOrderedAssignmentsByActivityAsync(
                activity.Id,
                cancellationToken
            );

            if (!currentAssignments.Any())
            {
                continue;
            }

            var excludedSlots = await GetExcludedSlotsAsync(
                activity,
                request.OrganizationId,
                request.SchedulingPeriodId
            );

            var isValid = await IsAssignmentSetValidAsync(
                activity,
                currentAssignments,
                excludedSlots,
                cancellationToken
            );
            if (isValid)
            {
                continue;
            }

            var rematchResult = await TryRescheduleAssignmentAsync(
                request.ActivityConstraintId,
                activity,
                currentAssignments,
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
        List<Assignment> currentAssignments,
        HashSet<Guid> excludedSlots,
        Guid organizationId,
        Guid schedulingPeriodId,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation("Re-matching Activity {ActivityId}", activity.Id);

        var allAssignments = await _assignmentRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId);
        var currentAssignmentIds = currentAssignments.Select(a => a.Id).ToHashSet();
        var occupiedPairs = allAssignments
            .Where(a => !currentAssignmentIds.Contains(a.Id))
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

        var startAssignment = currentAssignments[0];

        // Stage A: try to preserve the starting slot while finding a valid full streak.
        var sameStartCandidates = await BuildStreakCandidatesAsync(
            activity,
            slots.Where(s => s.Id == startAssignment.SlotId && !excludedSlots.Contains(s.Id)).ToList(),
            slots,
            resources,
            occupiedPairs,
            excludedSlots,
            cancellationToken
        );

        if (sameStartCandidates.Any())
        {
            var sameSlotSelection = await SelectCandidateAsync(
                sameStartCandidates,
                activity,
                organizationId,
                schedulingPeriodId
            );

            foreach (var assignment in currentAssignments)
            {
                await _assignmentRepository.DeleteAsync(assignment);
            }

            foreach (var pair in sameSlotSelection.Streak.OrderBy(p => p.Slot.FromTime))
            {
                await _assignmentRepository.AddAsync(new Assignment
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    SlotId = pair.SlotId,
                    ResourceId = pair.ResourceId,
                    ActivityId = activity.Id,
                });
            }

            return new SchedulingResult(
                requestId,
                true,
                0,
                1,
                new List<Guid>(),
                "Activity successfully re-matched within the same timeslot"
            );
        }

        // Stage B: fallback to another starting slot and resource.
        var fallbackCandidates = await BuildStreakCandidatesAsync(
            activity,
            slots
                .Where(s => s.Id != startAssignment.SlotId)
                .Where(s => !excludedSlots.Contains(s.Id))
                .ToList(),
            slots,
            resources,
            occupiedPairs,
            excludedSlots,
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

        foreach (var assignment in currentAssignments)
        {
            await _assignmentRepository.DeleteAsync(assignment);
        }

        foreach (var pair in fallbackSelection.Streak.OrderBy(p => p.Slot.FromTime))
        {
            await _assignmentRepository.AddAsync(new Assignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SlotId = pair.SlotId,
                ResourceId = pair.ResourceId,
                ActivityId = activity.Id,
            });
        }

        _logger.LogInformation(
            "Successfully re-matched Activity {ActivityId} using fallback stage - New streak length: {StreakLength}, Resource {ResourceId}",
            activity.Id,
            fallbackSelection.Streak.Count,
            fallbackSelection.Anchor.ResourceId
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

    private async Task<List<(SlotResourcePair Anchor, List<SlotResourcePair> Streak)>> BuildStreakCandidatesAsync(
        Activity activity,
        List<Slot> startSlots,
        List<Slot> allSlots,
        List<Resource> resources,
        HashSet<(Guid SlotId, Guid ResourceId)> occupiedPairs,
        HashSet<Guid> excludedSlots,
        CancellationToken cancellationToken
    )
    {
        var candidates = new List<(SlotResourcePair Anchor, List<SlotResourcePair> Streak)>();
        var dedupe = new HashSet<string>();
        var orderedAllSlots = allSlots
            .OrderBy(s => s.Weekday)
            .ThenBy(s => s.FromTime)
            .ThenBy(s => s.ToTime)
            .ToList();

        foreach (var slot in startSlots)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            foreach (var resource in resources)
            {
                var key = (slot.Id, resource.Id);
                if (excludedSlots.Contains(slot.Id) || occupiedPairs.Contains(key))
                {
                    continue;
                }

                var canAssign = await _constraintEvaluator.CanAssignAsync(activity, slot, resource);
                if (!canAssign)
                {
                    continue;
                }

                var anchor = new SlotResourcePair(slot, resource);
                var streak = await TryBuildConsecutiveStreakAsync(
                    activity,
                    slot,
                    resource,
                    orderedAllSlots,
                    excludedSlots,
                    occupiedPairs,
                    cancellationToken
                );

                if (streak == null || streak.Count == 0)
                {
                    continue;
                }

                var dedupeKey = $"{resource.Id}:{string.Join("|", streak.Select(s => s.SlotId))}";
                if (!dedupe.Add(dedupeKey))
                {
                    continue;
                }

                candidates.Add((anchor, streak));
            }
        }

        return candidates;
    }

    private async Task<(SlotResourcePair Anchor, List<SlotResourcePair> Streak)> SelectCandidateAsync(
        List<(SlotResourcePair Anchor, List<SlotResourcePair> Streak)> candidates,
        Activity activity,
        Guid organizationId,
        Guid schedulingPeriodId
    )
    {
        var weights = new double[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
        {
            weights[i] = await _ranker.CalculateWeightAsync(
                candidates[i].Anchor,
                activity.AssignedUserId,
                organizationId,
                schedulingPeriodId
            );
        }

        var anchors = candidates.Select(c => c.Anchor).ToList();
        var selectedAnchor = _ranker.SelectRandomWeighted(anchors, weights);
        return candidates.First(c => c.Anchor == selectedAnchor);
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

    private async Task<bool> IsAssignmentSetValidAsync(
        Activity activity,
        List<Assignment> assignments,
        HashSet<Guid> excludedSlots,
        CancellationToken cancellationToken
    )
    {
        if (assignments.Count == 0)
        {
            return false;
        }

        var slotIds = assignments.Select(a => a.SlotId).Distinct().ToList();
        var resourceIds = assignments.Select(a => a.ResourceId).Distinct().ToList();

        if (resourceIds.Count != 1)
        {
            return false;
        }

        if (slotIds.Any(excludedSlots.Contains))
        {
            return false;
        }

        var slots = await _dbContext
            .Slots.IgnoreQueryFilters()
            .Where(s => slotIds.Contains(s.Id))
            .ToListAsync(cancellationToken);

        if (slots.Count != slotIds.Count)
        {
            return false;
        }

        var resource = await _dbContext
            .Resources.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == resourceIds[0], cancellationToken);

        if (resource == null)
        {
            return false;
        }

        var orderedSlots = slots
            .OrderBy(s => s.FromTime)
            .ThenBy(s => s.ToTime)
            .ToList();

        if (orderedSlots.Select(s => s.Weekday).Distinct().Count() != 1)
        {
            return false;
        }

        var requiredDuration = GetRequiredDuration(activity);
        var totalDuration = orderedSlots.Aggregate(TimeSpan.Zero, (sum, slot) => sum + GetSlotDuration(slot));
        if (requiredDuration <= TimeSpan.Zero || totalDuration != requiredDuration)
        {
            return false;
        }

        for (int i = 1; i < orderedSlots.Count; i++)
        {
            if (!AreConsecutive(orderedSlots[i - 1], orderedSlots[i]))
            {
                return false;
            }
        }

        foreach (var slot in orderedSlots)
        {
            var canAssign = await _constraintEvaluator.CanAssignAsync(activity, slot, resource);
            if (!canAssign)
            {
                return false;
            }
        }

        return true;
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

    private async Task<List<Assignment>> GetOrderedAssignmentsByActivityAsync(
        Guid activityId,
        CancellationToken cancellationToken
    )
    {
        var assignments = await _assignmentRepository.GetByActivityIdAsync(activityId);
        if (!assignments.Any())
        {
            return assignments;
        }

        var slotsById = await _dbContext
            .Slots.IgnoreQueryFilters()
            .Where(s => assignments.Select(a => a.SlotId).Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        return assignments
            .Where(a => slotsById.ContainsKey(a.SlotId))
            .OrderBy(a => slotsById[a.SlotId].Weekday)
            .ThenBy(a => slotsById[a.SlotId].FromTime)
            .ThenBy(a => slotsById[a.SlotId].ToTime)
            .ToList();
    }

    private async Task<List<SlotResourcePair>?> TryBuildConsecutiveStreakAsync(
        Activity activity,
        Slot startSlot,
        Resource resource,
        List<Slot> orderedSlots,
        HashSet<Guid> excludedSlots,
        HashSet<(Guid SlotId, Guid ResourceId)> occupiedPairs,
        CancellationToken cancellationToken
    )
    {
        var requiredDuration = GetRequiredDuration(activity);
        var startDuration = GetSlotDuration(startSlot);

        if (requiredDuration <= TimeSpan.Zero || startDuration <= TimeSpan.Zero || startDuration > requiredDuration)
        {
            return null;
        }

        var streak = new List<SlotResourcePair> { new(startSlot, resource) };
        var currentSlot = startSlot;
        var totalDuration = startDuration;

        while (totalDuration < requiredDuration)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var nextSlot = orderedSlots.FirstOrDefault(s => AreConsecutive(currentSlot, s));
            if (nextSlot == null)
            {
                return null;
            }

            if (excludedSlots.Contains(nextSlot.Id) || occupiedPairs.Contains((nextSlot.Id, resource.Id)))
            {
                return null;
            }

            var canAssign = await _constraintEvaluator.CanAssignAsync(activity, nextSlot, resource);
            if (!canAssign)
            {
                return null;
            }

            var nextDuration = GetSlotDuration(nextSlot);
            if (nextDuration <= TimeSpan.Zero)
            {
                return null;
            }

            streak.Add(new SlotResourcePair(nextSlot, resource));
            totalDuration += nextDuration;
            currentSlot = nextSlot;
        }

        return totalDuration == requiredDuration ? streak : null;
    }

    private static TimeSpan GetRequiredDuration(Activity activity)
    {
        if (activity.Duration <= 0)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromMinutes(activity.Duration);
    }

    private static TimeSpan GetSlotDuration(Slot slot)
    {
        return slot.ToTime - slot.FromTime;
    }

    private static bool AreConsecutive(Slot current, Slot next)
    {
        return current.Weekday == next.Weekday && current.ToTime == next.FromTime;
    }
}
