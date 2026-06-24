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
                $"Algorithm failed: {ex.Message}",
                constraintRequest.InitiatedByUserId
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
                "Activity id could not be resolved from constraint event",
                request.InitiatedByUserId
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
                "Activity not found",
                request.InitiatedByUserId
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
                "Activity not currently assigned",
                request.InitiatedByUserId
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
                "Could not resolve scheduling period for assignment",
                request.InitiatedByUserId
            );
        }

        var schedulingPeriod = await _dbContext.SchedulingPeriods
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == schedulingPeriodId, cancellationToken);

        List<int?> weeks;
        if (assignments.Any(a => a.WeekNum == null))
        {
            weeks = new List<int?> { null };
        }
        else if (schedulingPeriod != null)
        {
            weeks = PeriodWeekCalculator.GetPeriodWeekIndices(schedulingPeriod.FromDate, schedulingPeriod.ToDate)
                .Select(w => (int?)w)
                .ToList();
        }
        else
        {
            weeks = assignments.Select(a => a.WeekNum).Distinct().ToList();
        }

        var slots = await _dbContext
            .Slots.IgnoreQueryFilters()
            .Where(s => s.SchedulingPeriodId == schedulingPeriodId)
            .Where(s => s.OrganizationId == request.OrganizationId)
            .ToListAsync(cancellationToken);
        var resources = await _dbContext
            .Resources.IgnoreQueryFilters()
            .Where(r => r.OrganizationId == request.OrganizationId)
            .ToListAsync(cancellationToken);

        // Pre-fetch excluded slots for each week
        var weekExcludedSlots = new Dictionary<int, HashSet<Guid>>();
        foreach (var weekNum in weeks)
        {
            weekExcludedSlots[weekNum ?? -1] = await GetExcludedSlotsAsync(
                activity,
                request.OrganizationId,
                schedulingPeriodId,
                weekNum
            );
        }

        var dbAssignments = (await _assignmentRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId)) ?? new List<Assignment>();
        var currentAssignmentIds = assignments.Select(a => a.Id).ToHashSet();
        var activityIds = dbAssignments.Select(a => a.ActivityId).Distinct().ToList();
        var teachersMap = await _dbContext.Activities
            .IgnoreQueryFilters()
            .Where(a => a.OrganizationId == request.OrganizationId && activityIds.Contains(a.Id))
            .Where(a => a.AssignedUserId != Guid.Empty)
            .ToDictionaryAsync(a => a.Id, a => a.AssignedUserId, cancellationToken);

        var preferredSlotResource = await FindBestConsistentSlotResourceAsync(
            activity,
            assignments,
            weeks,
            request.OrganizationId,
            schedulingPeriodId,
            cancellationToken
        );

        var modifiedCount = 0;
        var unresolved = false;

        foreach (var weekNum in weeks)
        {
            var weekAssignments = assignments.Where(a => a.WeekNum == weekNum).ToList();
            var excludedSlots = weekExcludedSlots.TryGetValue(weekNum ?? -1, out var excl) ? excl : new HashSet<Guid>();

            if (weekAssignments.Any() && await IsPartialAssignmentStreakAsync(activity, weekAssignments, cancellationToken))
            {
                foreach (var assignment in weekAssignments.ToList())
                {
                    await _assignmentRepository.DeleteAsync(assignment);
                }

                unresolved = true;
                continue;
            }

            var isValid = await IsAssignmentSetValidAsync(
                activity,
                weekAssignments,
                excludedSlots,
                weekNum,
                cancellationToken
            );

            if (isValid && preferredSlotResource != null && weekAssignments.Any())
            {
                if (weekAssignments[0].SlotId != preferredSlotResource.SlotId ||
                    weekAssignments[0].ResourceId != preferredSlotResource.ResourceId)
                {
                    isValid = false;
                }
            }

            if (isValid)
            {
                continue;
            }

            var rescheduleResult = await TryRescheduleAssignmentAsync(
                request.ActivityConstraintId,
                activity,
                weekAssignments,
                excludedSlots,
                request.OrganizationId,
                schedulingPeriodId,
                weekNum,
                request.InitiatedByUserId,
                preferredSlotResource,
                cancellationToken
            );

            if (rescheduleResult.Success)
            {
                modifiedCount += rescheduleResult.AssignmentsModified;
                preferredSlotResource ??= rescheduleResult.SelectedAnchor;
            }
            else
            {
                unresolved = true;
            }

            var remainingForWeek = (await _assignmentRepository.GetByActivityIdAsync(activity.Id))
                .Where(a => a.WeekNum == weekNum)
                .ToList();
            if (remainingForWeek.Count > 0
                && await IsPartialAssignmentStreakAsync(activity, remainingForWeek, cancellationToken))
            {
                foreach (var assignment in remainingForWeek)
                {
                    await _assignmentRepository.DeleteAsync(assignment);
                }
                unresolved = true;
            }
        }

        if (unresolved)
        {
            return new SchedulingResult(
                request.ActivityConstraintId,
                false,
                0,
                modifiedCount,
                new List<Guid> { activity.Id },
                "Activity could not be re-scheduled for one or more weeks",
                request.InitiatedByUserId
            );
        }

        return new SchedulingResult(
            request.ActivityConstraintId,
            true,
            0,
            modifiedCount,
            new List<Guid>(),
            modifiedCount > 0
                ? "Activity successfully re-matched"
                : "Current assignment remains valid",
            request.InitiatedByUserId
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
                "User-scope event missing user id",
                request.InitiatedByUserId
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
                "User-scope event missing scheduling period id",
                request.InitiatedByUserId
            );
        }

        var assignments = (await _assignmentRepository.GetBySchedulingPeriodIdAsync(
            request.SchedulingPeriodId
        )) ?? new List<Assignment>();

        if (!assignments.Any())
        {
            return new SchedulingResult(
                request.ActivityConstraintId,
                true,
                0,
                0,
                new List<Guid>(),
                "No assignments found in scheduling period",
                request.InitiatedByUserId
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
                "No assigned activities were affected by this user constraint",
                request.InitiatedByUserId
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
            var allCurrentAssignments = await GetOrderedAssignmentsByActivityAsync(
                activity.Id,
                cancellationToken
            );

            if (!allCurrentAssignments.Any())
            {
                continue;
            }

            var schedulingPeriod = await _dbContext.SchedulingPeriods
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == request.SchedulingPeriodId, cancellationToken);

            List<int?> weeks;
            if (allCurrentAssignments.Any(a => a.WeekNum == null))
            {
                weeks = new List<int?> { null };
            }
            else if (schedulingPeriod != null)
            {
                weeks = PeriodWeekCalculator.GetPeriodWeekIndices(schedulingPeriod.FromDate, schedulingPeriod.ToDate)
                    .Select(w => (int?)w)
                    .ToList();
            }
            else
            {
                weeks = allCurrentAssignments.Select(a => a.WeekNum).Distinct().ToList();
            }

            var preferredSlotResource = await FindBestConsistentSlotResourceAsync(
                activity,
                allCurrentAssignments,
                weeks,
                request.OrganizationId,
                request.SchedulingPeriodId,
                cancellationToken
            );

            foreach (var weekNum in weeks)
            {
                var currentAssignments = allCurrentAssignments.Where(a => a.WeekNum == weekNum).ToList();

                var excludedSlots = await GetExcludedSlotsAsync(
                    activity,
                    request.OrganizationId,
                    request.SchedulingPeriodId,
                    weekNum
                );

                if (currentAssignments.Any() && await IsPartialAssignmentStreakAsync(activity, currentAssignments, cancellationToken))
                {
                    foreach (var assignment in currentAssignments.ToList())
                    {
                        await _assignmentRepository.DeleteAsync(assignment);
                    }
                    if (!unresolvedActivityIds.Contains(activity.Id))
                    {
                        unresolvedActivityIds.Add(activity.Id);
                    }
                    continue;
                }

                var isValid = await IsAssignmentSetValidAsync(
                    activity,
                    currentAssignments,
                    excludedSlots,
                    weekNum,
                    cancellationToken
                );

                if (isValid && preferredSlotResource != null && currentAssignments.Any())
                {
                    if (currentAssignments[0].SlotId != preferredSlotResource.SlotId ||
                        currentAssignments[0].ResourceId != preferredSlotResource.ResourceId)
                    {
                        isValid = false;
                    }
                }

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
                    weekNum,
                    request.InitiatedByUserId,
                    preferredSlotResource,
                    cancellationToken
                );

                if (rematchResult.Success)
                {
                    modifiedCount += rematchResult.AssignmentsModified;
                    preferredSlotResource ??= rematchResult.SelectedAnchor;
                }
                else if (!unresolvedActivityIds.Contains(activity.Id))
                {
                    unresolvedActivityIds.Add(activity.Id);
                }

                var remainingForWeek = (await _assignmentRepository.GetByActivityIdAsync(activity.Id))
                    .Where(a => a.WeekNum == weekNum)
                    .ToList();
                if (remainingForWeek.Count > 0
                    && await IsPartialAssignmentStreakAsync(activity, remainingForWeek, cancellationToken))
                {
                    foreach (var assignment in remainingForWeek)
                    {
                        await _assignmentRepository.DeleteAsync(assignment);
                    }
                    if (!unresolvedActivityIds.Contains(activity.Id))
                    {
                        unresolvedActivityIds.Add(activity.Id);
                    }
                }
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
            reason,
            request.InitiatedByUserId
        );
    }

    private record RescheduleResult(
        bool Success,
        int AssignmentsModified,
        SlotResourcePair? SelectedAnchor
    );

    private async Task<RescheduleResult> TryRescheduleAssignmentAsync(
        Guid requestId,
        Activity activity,
        List<Assignment> currentAssignments,
        HashSet<Guid> excludedSlots,
        Guid organizationId,
        Guid schedulingPeriodId,
        int? weekNum,
        Guid? initiatedByUserId,
        SlotResourcePair? preferredSlotResource,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation("Re-matching Activity {ActivityId} for week {WeekNum}", activity.Id, weekNum);

        var startAssignment = currentAssignments.FirstOrDefault();

        var allAssignments = (await _assignmentRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId)) ?? new List<Assignment>();
        var currentAssignmentIds = currentAssignments.Select(a => a.Id).ToHashSet();
        var occupiedPairs = allAssignments
            .Where(a => !currentAssignmentIds.Contains(a.Id))
            .Where(a => a.WeekNum == weekNum || a.WeekNum == null || weekNum == null)
            .Select(a => (a.SlotId, a.ResourceId))
            .ToHashSet();

        var occupiedTeachers = new HashSet<(Guid SlotId, Guid AssignedUserId)>();
        if (activity.AssignedUserId != Guid.Empty)
        {
            var activityIds = allAssignments
                .Where(a => !currentAssignmentIds.Contains(a.Id))
                .Where(a => a.WeekNum == weekNum || a.WeekNum == null || weekNum == null)
                .Select(a => a.ActivityId)
                .Distinct()
                .ToList();

            var teachersMap = await _dbContext.Activities
                .IgnoreQueryFilters()
                .Where(a => a.OrganizationId == organizationId && activityIds.Contains(a.Id))
                .Where(a => a.AssignedUserId != Guid.Empty)
                .ToDictionaryAsync(a => a.Id, a => a.AssignedUserId, cancellationToken);

            occupiedTeachers = allAssignments
                .Where(a => !currentAssignmentIds.Contains(a.Id))
                .Where(a => a.WeekNum == weekNum || a.WeekNum == null || weekNum == null)
                .Where(a => teachersMap.ContainsKey(a.ActivityId))
                .Select(a => (a.SlotId, teachersMap[a.ActivityId]))
                .ToHashSet();
        }

        var slots = await _dbContext
            .Slots.IgnoreQueryFilters()
            .Where(s => s.SchedulingPeriodId == schedulingPeriodId)
            .Where(s => s.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);
        var resources = await _dbContext
            .Resources.IgnoreQueryFilters()
            .Where(r => r.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

        // Stage 0: try to use the preferred slot and resource if specified
        if (preferredSlotResource != null)
        {
            var preferredStartSlots = slots.Where(s => s.Id == preferredSlotResource.SlotId && !excludedSlots.Contains(s.Id)).ToList();
            var preferredResources = resources.Where(r => r.Id == preferredSlotResource.ResourceId).ToList();

            var preferredCandidates = await BuildStreakCandidatesAsync(
                activity,
                preferredStartSlots,
                slots,
                preferredResources,
                occupiedPairs,
                occupiedTeachers,
                excludedSlots,
                weekNum,
                cancellationToken
            );

            if (preferredCandidates.Any())
            {
                var preferredSelection = await SelectCandidateAsync(
                    preferredCandidates,
                    activity,
                    organizationId,
                    schedulingPeriodId
                );

                foreach (var assignment in currentAssignments)
                {
                    await _assignmentRepository.DeleteAsync(assignment);
                }

                foreach (var pair in preferredSelection.Streak.OrderBy(p => p.Slot.FromTime))
                {
                    await _assignmentRepository.AddAsync(new Assignment
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = organizationId,
                        SlotId = pair.SlotId,
                        ResourceId = pair.ResourceId,
                        ActivityId = activity.Id,
                        WeekNum = weekNum,
                    });
                }

                return new RescheduleResult(true, 1, preferredSelection.Anchor);
            }
        }

        // Stage A: try to preserve the starting slot while finding a valid full streak.
        var sameStartCandidates = startAssignment != null
            ? await BuildStreakCandidatesAsync(
                activity,
                slots.Where(s => s.Id == startAssignment.SlotId && !excludedSlots.Contains(s.Id)).ToList(),
                slots,
                resources,
                occupiedPairs,
                occupiedTeachers,
                excludedSlots,
                weekNum,
                cancellationToken
            )
            : new List<(SlotResourcePair Anchor, List<SlotResourcePair> Streak)>();

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
                    WeekNum = weekNum,
                });
            }

            return new RescheduleResult(
                true,
                1,
                sameSlotSelection.Anchor
            );
        }

        // Stage B: fallback to another starting slot and resource.
        var fallbackCandidates = await BuildStreakCandidatesAsync(
            activity,
            slots
                .Where(s => startAssignment == null || s.Id != startAssignment.SlotId)
                .Where(s => !excludedSlots.Contains(s.Id))
                .ToList(),
            slots,
            resources,
            occupiedPairs,
            occupiedTeachers,
            excludedSlots,
            weekNum,
            cancellationToken
        );

        if (!fallbackCandidates.Any())
        {
            _logger.LogError(
                "No available replacement found for Activity {ActivityId}. Removing invalid assignment and reporting unresolved change.",
                activity.Id
            );

            foreach (var assignment in currentAssignments)
            {
                await _assignmentRepository.DeleteAsync(assignment);
            }

            return new RescheduleResult(
                false,
                0,
                null
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
                WeekNum = weekNum,
            });
        }

        _logger.LogInformation(
            "Successfully re-matched Activity {ActivityId} using fallback stage - New streak length: {StreakLength}, Resource {ResourceId}",
            activity.Id,
            fallbackSelection.Streak.Count,
            fallbackSelection.Anchor.ResourceId
        );

        return new RescheduleResult(
            true,
            1,
            fallbackSelection.Anchor
        );
    }

    private async Task<List<(SlotResourcePair Anchor, List<SlotResourcePair> Streak)>> BuildStreakCandidatesAsync(
        Activity activity,
        List<Slot> startSlots,
        List<Slot> allSlots,
        List<Resource> resources,
        HashSet<(Guid SlotId, Guid ResourceId)> occupiedPairs,
        HashSet<(Guid SlotId, Guid AssignedUserId)> occupiedTeachers,
        HashSet<Guid> excludedSlots,
        int? weekNum,
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

                if (activity.AssignedUserId != Guid.Empty
                    && occupiedTeachers.Contains((slot.Id, activity.AssignedUserId)))
                {
                    continue;
                }

                var canAssign = await _constraintEvaluator.CanAssignAsync(activity, slot, resource, weekNum);
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
                    occupiedTeachers,
                    weekNum,
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

    private async Task<bool> IsPartialAssignmentStreakAsync(
        Activity activity,
        List<Assignment> assignments,
        CancellationToken cancellationToken
    )
    {
        if (activity.Duration <= 0 || assignments.Count == 0)
        {
            return false;
        }

        var slotIds = assignments.Select(a => a.SlotId).Distinct().ToList();
        var slots = await _dbContext
            .Slots.IgnoreQueryFilters()
            .Where(s => slotIds.Contains(s.Id))
            .ToListAsync(cancellationToken);

        if (slots.Count != slotIds.Count)
        {
            return true;
        }

        var totalMinutes = assignments.Sum(a =>
        {
            var slot = slots.First(s => s.Id == a.SlotId);
            return (int)(slot.ToTime - slot.FromTime).TotalMinutes;
        });

        return totalMinutes != activity.Duration;
    }

    private async Task<HashSet<Guid>> GetExcludedSlotsAsync(
        Activity activity,
        Guid organizationId,
        Guid schedulingPeriodId,
        int? weekNum = null
    )
    {
        return await _constraintProcessor.GetExcludedSlotIdsAsync(
            activity.Id,
            organizationId,
            activity.AssignedUserId != Guid.Empty ? activity.AssignedUserId : null,
            schedulingPeriodId,
            weekNum
        );
    }

    private async Task<bool> IsAssignmentSetValidAsync(
        Activity activity,
        List<Assignment> assignments,
        HashSet<Guid> excludedSlots,
        int? weekNum,
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
            var canAssign = await _constraintEvaluator.CanAssignAsync(activity, slot, resource, weekNum);
            if (!canAssign)
            {
                return false;
            }
        }

        // Check if teacher is occupied by another activity at any of these slots
        if (activity.AssignedUserId != Guid.Empty)
        {
            var teacherOccupied = await _dbContext.Assignments
                .IgnoreQueryFilters()
                .Where(a => a.WeekNum == weekNum || a.WeekNum == null || weekNum == null)
                .Where(a => slotIds.Contains(a.SlotId))
                .Where(a => !assignments.Select(asg => asg.Id).Contains(a.Id))
                .Join(
                    _dbContext.Activities.IgnoreQueryFilters().Where(act => act.AssignedUserId == activity.AssignedUserId),
                    a => a.ActivityId,
                    act => act.Id,
                    (a, act) => a
                )
                .AnyAsync(cancellationToken);

            if (teacherOccupied)
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
        HashSet<(Guid SlotId, Guid AssignedUserId)> occupiedTeachers,
        int? weekNum,
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

            if (activity.AssignedUserId != Guid.Empty
                && occupiedTeachers.Contains((nextSlot.Id, activity.AssignedUserId)))
            {
                return null;
            }

            var canAssign = await _constraintEvaluator.CanAssignAsync(activity, nextSlot, resource, weekNum);
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

    private async Task<SlotResourcePair?> FindBestConsistentSlotResourceAsync(
        Activity activity,
        List<Assignment> currentAssignments,
        List<int?> weeks,
        Guid organizationId,
        Guid schedulingPeriodId,
        CancellationToken cancellationToken
    )
    {
        var slots = await _dbContext
            .Slots.IgnoreQueryFilters()
            .Where(s => s.SchedulingPeriodId == schedulingPeriodId)
            .Where(s => s.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);
        var resources = await _dbContext
            .Resources.IgnoreQueryFilters()
            .Where(r => r.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

        var orderedAllSlots = slots
            .OrderBy(s => s.Weekday)
            .ThenBy(s => s.FromTime)
            .ThenBy(s => s.ToTime)
            .ToList();

        var currentAssignmentIds = currentAssignments.Select(a => a.Id).ToHashSet();

        if (!weeks.Any())
        {
            return null;
        }
 
        var allAssignments = (await _assignmentRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId)) ?? new List<Assignment>();
        var activityIds = allAssignments.Select(a => a.ActivityId).Distinct().ToList();
        var teachersMap = await _dbContext.Activities
            .IgnoreQueryFilters()
            .Where(a => a.OrganizationId == organizationId && activityIds.Contains(a.Id))
            .Where(a => a.AssignedUserId != Guid.Empty)
            .ToDictionaryAsync(a => a.Id, a => a.AssignedUserId, cancellationToken);
 
        // Pre-fetch excluded slots for each week
        var weekExcludedSlots = new Dictionary<int, HashSet<Guid>>();
        foreach (var w in weeks)
        {
            weekExcludedSlots[w ?? -1] = await GetExcludedSlotsAsync(activity, organizationId, schedulingPeriodId, w);
        }

        // Try to check if the current starting slot and resource is valid for ALL weeks first
        var startAssignment = currentAssignments.FirstOrDefault();
        if (startAssignment != null)
        {
            var origSlot = slots.FirstOrDefault(s => s.Id == startAssignment.SlotId);
            var origResource = resources.FirstOrDefault(r => r.Id == startAssignment.ResourceId);
            if (origSlot != null && origResource != null)
            {
                var isOrigValidForAll = true;
                foreach (var w in weeks)
                {
                    var excludedSlots = weekExcludedSlots[w ?? -1];
                    var isValid = await IsAssignmentSetValidAsync(activity, currentAssignments.Where(a => a.WeekNum == w).ToList(), excludedSlots, w, cancellationToken);
                    if (!isValid)
                    {
                        isOrigValidForAll = false;
                        break;
                    }
                }

                if (isOrigValidForAll)
                {
                    return new SlotResourcePair(origSlot, origResource);
                }
            }
        }

        // Search for a candidate slot & resource that works for ALL weeks
        var candidates = new List<(SlotResourcePair Anchor, int ValidWeeksCount)>();
        foreach (var startSlot in slots)
        {
            foreach (var resource in resources)
            {
                var streakSlots = GetStaticConsecutiveStreak(activity, startSlot, orderedAllSlots);
                if (streakSlots == null || !streakSlots.Any()) continue;

                var validWeeksCount = 0;
                foreach (var w in weeks)
                {
                    var excludedSlots = weekExcludedSlots[w ?? -1];
                    var occupiedPairs = allAssignments
                        .Where(a => !currentAssignmentIds.Contains(a.Id))
                        .Where(a => a.WeekNum == w || a.WeekNum == null || w == null)
                        .Select(a => (a.SlotId, a.ResourceId))
                        .ToHashSet();

                    var occupiedTeachers = allAssignments
                        .Where(a => !currentAssignmentIds.Contains(a.Id))
                        .Where(a => a.WeekNum == w || a.WeekNum == null || w == null)
                        .Where(a => teachersMap.ContainsKey(a.ActivityId))
                        .Select(a => (a.SlotId, teachersMap[a.ActivityId]))
                        .ToHashSet();

                    var isWeekValid = true;
                    foreach (var slot in streakSlots)
                    {
                        if (excludedSlots.Contains(slot.Id) || occupiedPairs.Contains((slot.Id, resource.Id)))
                        {
                            isWeekValid = false;
                            break;
                        }

                        if (activity.AssignedUserId != Guid.Empty && occupiedTeachers.Contains((slot.Id, activity.AssignedUserId)))
                        {
                            isWeekValid = false;
                            break;
                        }

                        var canAssign = await _constraintEvaluator.CanAssignAsync(activity, slot, resource, w);
                        if (!canAssign)
                        {
                            isWeekValid = false;
                            break;
                        }
                    }

                    if (isWeekValid)
                    {
                        validWeeksCount++;
                    }
                }

                if (validWeeksCount == weeks.Count)
                {
                    return new SlotResourcePair(startSlot, resource);
                }
                else if (validWeeksCount > 0)
                {
                    candidates.Add((new SlotResourcePair(startSlot, resource), validWeeksCount));
                }
            }
        }

        if (candidates.Any())
        {
            return candidates.OrderByDescending(c => c.ValidWeeksCount).First().Anchor;
        }

        return null;
    }

    private List<Slot>? GetStaticConsecutiveStreak(
        Activity activity,
        Slot startSlot,
        List<Slot> orderedSlots
    )
    {
        var requiredDuration = GetRequiredDuration(activity);
        var startDuration = GetSlotDuration(startSlot);

        if (requiredDuration <= TimeSpan.Zero || startDuration <= TimeSpan.Zero || startDuration > requiredDuration)
        {
            return null;
        }

        var streak = new List<Slot> { startSlot };
        var currentSlot = startSlot;
        var totalDuration = startDuration;

        while (totalDuration < requiredDuration)
        {
            var nextSlot = orderedSlots.FirstOrDefault(s => AreConsecutive(currentSlot, s));
            if (nextSlot == null)
            {
                return null;
            }

            var nextDuration = GetSlotDuration(nextSlot);
            if (nextDuration <= TimeSpan.Zero)
            {
                return null;
            }

            streak.Add(nextSlot);
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
