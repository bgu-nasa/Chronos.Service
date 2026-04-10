using Chronos.Data.Context;
using Chronos.Data.Repositories.Resources;
using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Resources;
using Chronos.Domain.Schedule;
using Chronos.Domain.Schedule.Messages;
using Chronos.Engine.Constraints;
using Chronos.Engine.Constraints.Evaluation;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Engine.Matching;

/// <summary>
/// Batch mode matching using Ranking Algorithm
/// Achieves 1-1/e ≈ 0.632 competitive ratio
/// </summary>
public class RankingAlgorithmStrategy(
    IActivityRepository activityRepository,
    ISlotRepository slotRepository,
    IResourceRepository resourceRepository,
    IAssignmentRepository assignmentRepository,
    IConstraintProcessor constraintProcessor,
    IConstraintEvaluator constraintEvaluator,
    PreferenceWeightedRanker ranker,
    AppDbContext dbContext,
    ILogger<RankingAlgorithmStrategy> logger
) : IMatchingStrategy
{
    private readonly IActivityRepository _activityRepository = activityRepository;
    private readonly ISlotRepository _slotRepository = slotRepository;
    private readonly IResourceRepository _resourceRepository = resourceRepository;
    private readonly IAssignmentRepository _assignmentRepository = assignmentRepository;
    private readonly IConstraintProcessor _constraintProcessor = constraintProcessor;
    private readonly IConstraintEvaluator _constraintEvaluator = constraintEvaluator;
    private readonly PreferenceWeightedRanker _ranker = ranker;
    private readonly AppDbContext _dbContext = dbContext;
    private readonly ILogger<RankingAlgorithmStrategy> _logger = logger;
    private readonly Random _random = new();

    public SchedulingMode Mode => SchedulingMode.Batch;

    public async Task<SchedulingResult> ExecuteAsync(
        object request,
        CancellationToken cancellationToken
    )
    {
        if (request is not SchedulePeriodRequest periodRequest)
        {
            throw new ArgumentException(
                $"Expected SchedulePeriodRequest, got {request.GetType().Name}"
            );
        }

        _logger.LogInformation(
            "Starting Ranking Algorithm for SchedulingPeriod {PeriodId}, Organization {OrgId}",
            periodRequest.SchedulingPeriodId,
            periodRequest.OrganizationId
        );

        try
        {
            var startTime = DateTime.UtcNow;
            _logger.LogInformation("Starting Ranking Algorithm execution at {StartTime}", startTime);

            // Delete all existing assignments for this period (Engine has no org filter, so we see and delete everything)
            await _assignmentRepository.DeleteBySchedulingPeriodIdAsync(periodRequest.SchedulingPeriodId);
            _logger.LogInformation(
                "Deleted all existing assignments for period {PeriodId} before batch run",
                periodRequest.SchedulingPeriodId
            );

            // Step 1: Load all activities for the scheduling period
            var activities = await LoadActivitiesForPeriodAsync(periodRequest.OrganizationId, periodRequest.SchedulingPeriodId);
            _logger.LogInformation(
                "Loaded {ActivityCount} activities to schedule",
                activities.Count
            );

            if (activities.Count == 0)
            {
                return new SchedulingResult(
                    periodRequest.SchedulingPeriodId,
                    true,
                    0,
                    0,
                    new List<Guid>(),
                    "No activities to schedule"
                );
            }

            // Step 2: Load all (Slot, Resource) pairs
            var slots = await _slotRepository.GetBySchedulingPeriodIdAsync(
                periodRequest.SchedulingPeriodId
            );
            var resources = await _resourceRepository.GetAllAsync();

            _logger.LogInformation(
                "Loaded {SlotCount} slots and {ResourceCount} resources",
                slots.Count,
                resources.Count
            );

            // Step 3: Generate all (Slot, Resource) combinations → Set L
            var allPairs = GenerateSlotResourcePairs(slots, resources);
            _logger.LogInformation("Generated {PairCount} (Slot, Resource) pairs", allPairs.Count);

            // Step 4: Generate RANDOM PERMUTATION σ of L (the "Ranking")
            var rankedPairs = GenerateRandomPermutation(allPairs);
            _logger.LogInformation(
                "Generated random permutation of {PairCount} pairs",
                rankedPairs.Count
            );

            // Step 5: Process activities and match using ranking algorithm
            var result = await ProcessActivitiesWithRankingAsync(
                activities,
                rankedPairs,
                periodRequest.OrganizationId,
                periodRequest.SchedulingPeriodId,
                cancellationToken
            );

            _logger.LogInformation(
                "Ranking Algorithm completed. Created: {Created}, Failed: {Failed}",
                result.AssignmentsCreated,
                result.UnscheduledActivityIds.Count
            );

            var endTime = DateTime.UtcNow;
            var duration = (endTime - startTime).TotalSeconds;
            _logger.LogInformation(
                "Ranking Algorithm execution time: {Duration:F2} seconds ({ActivitiesPerSecond:F2} activities/sec)",
                duration,
                activities.Count / Math.Max(duration, 0.001)
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Ranking Algorithm");
            return new SchedulingResult(
                periodRequest.SchedulingPeriodId,
                false,
                0,
                0,
                new List<Guid>(),
                $"Algorithm failed: {ex.Message}"
            );
        }
    }

    private async Task<List<Activity>> LoadActivitiesForPeriodAsync(Guid organizationId, Guid schedulingPeriodId)
    {
        // Load activities filtered by organization and scheduling period
        // Activities are linked to Subjects, which have SchedulingPeriodId
        // We need to bypass the organization query filter since we're a background worker
        // and don't have an HTTP context with organization ID
        
        // Load activities by joining with subjects to filter by scheduling period
        // Use IgnoreQueryFilters to bypass the organization query filter since we're filtering manually
        // Also include subjects with empty GUID (00000000-0000-0000-0000-000000000000) as a fallback
        // to handle subjects that were created without being assigned to a scheduling period yet
        var emptyGuid = Guid.Empty;
        var activities = await _dbContext.Activities
            .IgnoreQueryFilters()
            .Where(a => a.OrganizationId == organizationId)
            .Join(
                _dbContext.Subjects.IgnoreQueryFilters()
                    .Where(s => s.OrganizationId == organizationId && 
                               (s.SchedulingPeriodId == schedulingPeriodId || s.SchedulingPeriodId == emptyGuid)),
                activity => activity.SubjectId,
                subject => subject.Id,
                (activity, subject) => activity
            )
            .ToListAsync();
        
        _logger.LogInformation(
            "Loaded {Count} activities for scheduling period {PeriodId} in organization {OrganizationId}",
            activities.Count, schedulingPeriodId, organizationId);
        
        return activities;
    }

    private List<SlotResourcePair> GenerateSlotResourcePairs(
        List<Slot> slots,
        List<Resource> resources
    )
    {
        var pairs = new List<SlotResourcePair>();

        foreach (var slot in slots)
        {
            foreach (var resource in resources)
            {
                pairs.Add(new SlotResourcePair(slot, resource));
            }
        }

        return pairs;
    }

    private List<SlotResourcePair> GenerateRandomPermutation(List<SlotResourcePair> pairs)
    {
        // Fisher-Yates shuffle to generate random permutation
        var permutation = new List<SlotResourcePair>(pairs);
        int n = permutation.Count;

        for (int i = n - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (permutation[i], permutation[j]) = (permutation[j], permutation[i]);
        }

        // Assign ranks based on position in permutation
        for (int i = 0; i < permutation.Count; i++)
        {
            permutation[i] = permutation[i] with { Rank = i + 1 };
        }

        return permutation;
    }

    private async Task<SchedulingResult> ProcessActivitiesWithRankingAsync(
        List<Activity> activities,
        List<SlotResourcePair> rankedPairs,
        Guid organizationId,
        Guid schedulingPeriodId,
        CancellationToken cancellationToken
    )
    {
        var createdAssignments = 0;
        var unscheduledActivities = new List<Guid>();
        var occupiedPairs = new HashSet<(Guid SlotId, Guid ResourceId)>();

        _logger.LogInformation(
            "Processing {ActivityCount} activities with {PairCount} ranked pairs",
            activities.Count,
            rankedPairs.Count
        );

        var activityIndex = 0;

        foreach (var activity in activities)
        {
            activityIndex++;
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Batch scheduling cancelled after processing {ProcessedCount}/{TotalCount} activities",
                    activityIndex - 1,
                    activities.Count
                );
                break;
            }

            _logger.LogInformation(
                "Processing Activity {ActivityId} ({Index}/{Total})",
                activity.Id,
                activityIndex,
                activities.Count
            );

            // Get excluded slots from constraints (including user constraints for the assigned lecturer)
            var excludedSlots = await _constraintProcessor.GetExcludedSlotIdsAsync(
                activity.Id,
                organizationId,
                activity.AssignedUserId != Guid.Empty ? activity.AssignedUserId : null,
                schedulingPeriodId
            );

            var requiredDuration = GetRequiredDuration(activity);
            if (requiredDuration <= TimeSpan.Zero)
            {
                _logger.LogWarning(
                    "Activity {ActivityId} has invalid duration {Duration}; skipping",
                    activity.Id,
                    activity.Duration
                );
                unscheduledActivities.Add(activity.Id);
                continue;
            }

            var slotsByResource = rankedPairs
                .GroupBy(p => p.ResourceId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(p => p.Slot)
                        .DistinctBy(s => s.Id)
                        .OrderBy(s => s.Weekday)
                        .ThenBy(s => s.FromTime)
                        .ThenBy(s => s.ToTime)
                        .ToList()
                );

            var pairByKey = rankedPairs
                .GroupBy(p => (p.SlotId, p.ResourceId))
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Rank).First());

            var streakCandidates = new List<(SlotResourcePair Anchor, List<SlotResourcePair> Streak)>();
            var dedupe = new HashSet<string>();

            foreach (var pair in rankedPairs.OrderBy(p => p.Rank))
            {
                if (excludedSlots.Contains(pair.SlotId))
                {
                    continue;
                }

                if (occupiedPairs.Contains((pair.SlotId, pair.ResourceId)))
                {
                    continue;
                }

                var canAssignStart = await _constraintEvaluator.CanAssignAsync(
                    activity,
                    pair.Slot,
                    pair.Resource
                );
                if (!canAssignStart)
                {
                    continue;
                }

                var streak = await TryBuildConsecutiveStreakAsync(
                    activity,
                    pair,
                    requiredDuration,
                    slotsByResource,
                    pairByKey,
                    excludedSlots,
                    occupiedPairs
                );

                if (streak == null || streak.Count == 0)
                {
                    continue;
                }

                var dedupeKey = $"{pair.ResourceId}:{string.Join("|", streak.Select(s => s.SlotId))}";
                if (!dedupe.Add(dedupeKey))
                {
                    continue;
                }

                streakCandidates.Add((pair, streak));
            }

            if (!streakCandidates.Any())
            {
                _logger.LogWarning(
                    "No valid consecutive streak candidates for Activity {ActivityId}. RequiredDuration: {RequiredDuration}, ExcludedSlots: {ExcludedCount}",
                    activity.Id,
                    requiredDuration,
                    excludedSlots.Count
                );

                unscheduledActivities.Add(activity.Id);
                continue;
            }

            _logger.LogInformation(
                "Found {CandidateCount} valid streak candidates for Activity {ActivityId}",
                streakCandidates.Count,
                activity.Id
            );

            var candidateWeights = new List<((SlotResourcePair Anchor, List<SlotResourcePair> Streak) Candidate, double Weight)>();

            foreach (var candidate in streakCandidates)
            {
                var weight = await _ranker.CalculateWeightAsync(
                    candidate.Anchor,
                    activity.AssignedUserId,
                    organizationId,
                    schedulingPeriodId
                );

                candidateWeights.Add((candidate, weight));
            }

            var orderedCandidates = candidateWeights
                .OrderByDescending(cw => cw.Weight)
                .ThenBy(cw => cw.Candidate.Anchor.Rank)
                .Select(cw => cw.Candidate)
                .ToList();

            var orderedWeights = candidateWeights
                .OrderByDescending(cw => cw.Weight)
                .ThenBy(cw => cw.Candidate.Anchor.Rank)
                .Select(cw => cw.Weight)
                .ToArray();

            _logger.LogDebug(
                "Ordered {CandidateCount} valid streak candidates by preference weight for Activity {ActivityId}. Top weight: {TopWeight:F2}",
                orderedCandidates.Count,
                activity.Id,
                orderedWeights.Length > 0 ? orderedWeights[0] : 0.0
            );

            if (orderedCandidates.Count == 0)
            {
                _logger.LogWarning(
                    "No valid streak candidates remaining after ordering for Activity {ActivityId}. Excluded slots: {ExcludedCount}",
                    activity.Id,
                    excludedSlots.Count
                );
                unscheduledActivities.Add(activity.Id);
                continue;
            }

            var excludedInOrdered = orderedCandidates
                .Where(c => c.Streak.Any(s => excludedSlots.Contains(s.SlotId)))
                .ToList();

            if (excludedInOrdered.Any())
            {
                _logger.LogError(
                    "CRITICAL: Found {Count} streak candidates containing excluded slots for Activity {ActivityId}. Removing them immediately.",
                    excludedInOrdered.Count,
                    activity.Id
                );

                var validOrdered = orderedCandidates
                    .Where(c => c.Streak.All(s => !excludedSlots.Contains(s.SlotId)))
                    .ToList();

                if (validOrdered.Count == 0)
                {
                    _logger.LogWarning(
                        "No valid streak candidates remaining after removing excluded slots for Activity {ActivityId}",
                        activity.Id
                    );
                    unscheduledActivities.Add(activity.Id);
                    continue;
                }

                var weightDict = candidateWeights.ToDictionary(cw => cw.Candidate, cw => cw.Weight);
                orderedCandidates = validOrdered;
                orderedWeights = validOrdered.Select(c => weightDict[c]).ToArray();
            }

            var selectedAnchor = _ranker.SelectRandomWeighted(
                orderedCandidates.Select(c => c.Anchor).ToList(),
                orderedWeights
            );

            var selected = orderedCandidates.First(c => c.Anchor == selectedAnchor);

            if (selected.Streak.Any(s => excludedSlots.Contains(s.SlotId)))
            {
                _logger.LogError(
                    "CRITICAL CONSTRAINT VIOLATION: Selected streak includes excluded slot(s) for Activity {ActivityId}. Excluded slots: {ExcludedCount}. This should never happen!",
                    activity.Id,
                    excludedSlots.Count
                );
                _logger.LogError(
                    "Excluded slot IDs: {SlotIds}",
                    string.Join(", ", excludedSlots)
                );
                unscheduledActivities.Add(activity.Id);
                continue;
            }

            _logger.LogInformation(
                "Matched Activity {ActivityId} to streak of {SlotCount} slots with Resource {ResourceId} (validated against {ExcludedCount} excluded slots)",
                activity.Id,
                selected.Streak.Count,
                selected.Anchor.ResourceId,
                excludedSlots.Count
            );

            foreach (var pair in selected.Streak.OrderBy(s => s.Slot.FromTime))
            {
                var assignment = new Assignment
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    SlotId = pair.SlotId,
                    ResourceId = pair.ResourceId,
                    ActivityId = activity.Id,
                };

                await _assignmentRepository.AddAsync(assignment);
                occupiedPairs.Add((pair.SlotId, pair.ResourceId));
                createdAssignments++;
            }

            if (createdAssignments % 10 == 0)
            {
                _logger.LogInformation(
                    "Progress: {Created} assignments created, {Failed} failed, {Remaining} remaining",
                    createdAssignments,
                    unscheduledActivities.Count,
                    activities.Count - activityIndex
                );
            }
        }

        return new SchedulingResult(
            schedulingPeriodId,
            true,
            createdAssignments,
            0,
            unscheduledActivities,
            unscheduledActivities.Any()
                ? $"{unscheduledActivities.Count} activities could not be scheduled"
                : null
        );
    }

    private static TimeSpan GetRequiredDuration(Activity activity)
    {
        if (activity.Duration <= 0)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromMinutes(activity.Duration);
    }

    private static bool AreConsecutive(Slot current, Slot next)
    {
        return current.Weekday == next.Weekday && current.ToTime == next.FromTime;
    }

    private static TimeSpan GetSlotDuration(Slot slot)
    {
        return slot.ToTime - slot.FromTime;
    }

    private async Task<List<SlotResourcePair>?> TryBuildConsecutiveStreakAsync(
        Activity activity,
        SlotResourcePair startPair,
        TimeSpan requiredDuration,
        Dictionary<Guid, List<Slot>> slotsByResource,
        Dictionary<(Guid SlotId, Guid ResourceId), SlotResourcePair> pairByKey,
        HashSet<Guid> excludedSlots,
        HashSet<(Guid SlotId, Guid ResourceId)> occupiedPairs
    )
    {
        if (!slotsByResource.TryGetValue(startPair.ResourceId, out var resourceSlots))
        {
            return null;
        }

        var startDuration = GetSlotDuration(startPair.Slot);
        if (startDuration <= TimeSpan.Zero || startDuration > requiredDuration)
        {
            return null;
        }

        var streak = new List<SlotResourcePair> { startPair };
        var totalDuration = startDuration;
        var currentSlot = startPair.Slot;

        while (totalDuration < requiredDuration)
        {
            var nextSlot = resourceSlots.FirstOrDefault(s => AreConsecutive(currentSlot, s));
            if (nextSlot == null)
            {
                return null;
            }

            if (excludedSlots.Contains(nextSlot.Id))
            {
                return null;
            }

            var nextKey = (nextSlot.Id, startPair.ResourceId);
            if (occupiedPairs.Contains(nextKey) || !pairByKey.TryGetValue(nextKey, out var nextPair))
            {
                return null;
            }

            var canAssign = await _constraintEvaluator.CanAssignAsync(
                activity,
                nextPair.Slot,
                nextPair.Resource
            );
            if (!canAssign)
            {
                return null;
            }

            var nextDuration = GetSlotDuration(nextSlot);
            if (nextDuration <= TimeSpan.Zero)
            {
                return null;
            }

            streak.Add(nextPair);
            totalDuration += nextDuration;
            currentSlot = nextSlot;
        }

        return totalDuration == requiredDuration ? streak : null;
    }

}
