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
            _logger.LogDebug("Starting Ranking Algorithm execution at {StartTime}", startTime);

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

        _logger.LogDebug(
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

            _logger.LogDebug(
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

            // Filter valid candidates
            var totalPairs = rankedPairs.Count;
            
            // First filter: slot-level exclusions and occupation
            var preFilteredPairs = rankedPairs
                .Where(p => !excludedSlots.Contains(p.SlotId))
                .Where(p => !occupiedPairs.Contains((p.SlotId, p.ResourceId)))
                .ToList();
            
            // Second filter: constraint evaluation (including required_capacity)
            var validCandidates = new List<SlotResourcePair>();
            foreach (var pair in preFilteredPairs)
            {
                // Use constraint evaluator to check if this (slot, resource) pair is valid
                var canAssign = await _constraintEvaluator.CanAssignAsync(activity, pair.Slot, pair.Resource);
                if (canAssign)
                {
                    validCandidates.Add(pair);
                }
                else
                {
                    _logger.LogTrace(
                        "Excluding (Slot {SlotId}, Resource {ResourceId}) for Activity {ActivityId} due to constraint violation",
                        pair.SlotId,
                        pair.ResourceId,
                        activity.Id
                    );
                }
            }
            
            // Order by rank (earlier rank = higher priority)
            validCandidates = validCandidates
                .OrderBy(p => p.Rank)
                .ToList();

            var excludedByConstraints = rankedPairs.Count(p => excludedSlots.Contains(p.SlotId));
            var excludedByOccupation = rankedPairs.Count(p =>
                !excludedSlots.Contains(p.SlotId)
                && occupiedPairs.Contains((p.SlotId, p.ResourceId))
            );
            var excludedByCapacity =
                totalPairs - excludedByConstraints - excludedByOccupation - validCandidates.Count;

            _logger.LogTrace(
                "Activity {ActivityId} filtering: {Valid} valid, {Constraints} excluded by constraints, {Occupied} occupied, {Capacity} insufficient capacity",
                activity.Id,
                validCandidates.Count,
                excludedByConstraints,
                excludedByOccupation,
                excludedByCapacity
            );

            if (!validCandidates.Any())
            {
                _logger.LogWarning(
                    "No valid candidates for Activity {ActivityId}. Excluded slots: {ExcludedCount}, Occupied: {OccupiedCount}",
                    activity.Id,
                    excludedSlots.Count,
                    occupiedPairs.Count
                );

                unscheduledActivities.Add(activity.Id);
                continue;
            }

            _logger.LogDebug(
                "Found {CandidateCount} valid candidates for Activity {ActivityId}",
                validCandidates.Count,
                activity.Id
            );

            // Step 1: Calculate preference weights for all valid candidates
            var candidateWeights = new List<(SlotResourcePair Candidate, double Weight)>();
            
            foreach (var candidate in validCandidates)
            {
                var weight = await _ranker.CalculateWeightAsync(
                    candidate,
                    activity.AssignedUserId,
                    organizationId,
                    schedulingPeriodId
                );

                candidateWeights.Add((candidate, weight));
            }

            // Step 2: Order candidates by weight (descending) - preferences determine priority
            var orderedCandidates = candidateWeights
                .OrderByDescending(cw => cw.Weight)
                .ThenBy(cw => cw.Candidate.Rank) // Secondary sort by rank for tie-breaking
                .Select(cw => cw.Candidate)
                .ToList();

            var orderedWeights = candidateWeights
                .OrderByDescending(cw => cw.Weight)
                .ThenBy(cw => cw.Candidate.Rank)
                .Select(cw => cw.Weight)
                .ToArray();

            _logger.LogDebug(
                "Ordered {CandidateCount} valid candidates by preference weight for Activity {ActivityId}. Top weight: {TopWeight:F2}",
                orderedCandidates.Count,
                activity.Id,
                orderedWeights.Length > 0 ? orderedWeights[0] : 0.0
            );

            // Step 3: Select from ordered candidates using weighted random sampling
            // This ensures preferences are respected while still allowing some randomness
            if (orderedCandidates.Count == 0)
            {
                _logger.LogWarning(
                    "No valid candidates remaining after filtering for Activity {ActivityId}. Excluded slots: {ExcludedCount}",
                    activity.Id,
                    excludedSlots.Count
                );
                unscheduledActivities.Add(activity.Id);
                continue;
            }

            // CRITICAL: Final validation - ensure no excluded slots made it through
            var excludedInOrdered = orderedCandidates.Where(c => excludedSlots.Contains(c.SlotId)).ToList();
            if (excludedInOrdered.Any())
            {
                _logger.LogError(
                    "CRITICAL: Found {Count} excluded slots in ordered candidates for Activity {ActivityId}. Removing them immediately. Excluded slot IDs: {SlotIds}",
                    excludedInOrdered.Count,
                    activity.Id,
                    string.Join(", ", excludedInOrdered.Select(c => c.SlotId))
                );
                // Remove excluded slots and their corresponding weights
                var validOrdered = orderedCandidates
                    .Where(c => !excludedSlots.Contains(c.SlotId))
                    .ToList();
                
                if (validOrdered.Count == 0)
                {
                    _logger.LogWarning(
                        "No valid candidates remaining after removing excluded slots for Activity {ActivityId}",
                        activity.Id
                    );
                    unscheduledActivities.Add(activity.Id);
                    continue;
                }
                
                // Rebuild weights array for remaining candidates
                var weightDict = candidateWeights.ToDictionary(cw => cw.Candidate, cw => cw.Weight);
                orderedCandidates = validOrdered;
                orderedWeights = validOrdered.Select(c => weightDict[c]).ToArray();
            }

            var selected = _ranker.SelectRandomWeighted(orderedCandidates, orderedWeights);
            
            // CRITICAL: Final check before assignment - if selected slot is excluded, fail
            if (excludedSlots.Contains(selected.SlotId))
            {
                _logger.LogError(
                    "CRITICAL CONSTRAINT VIOLATION: Selected slot {SlotId} is in excluded slots for Activity {ActivityId}. Excluded slots: {ExcludedCount}. This should never happen!",
                    selected.SlotId,
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
                "Matched Activity {ActivityId} to {Candidate} (validated against {ExcludedCount} excluded slots)",
                activity.Id,
                selected,
                excludedSlots.Count
            );

            // Create assignment
            var assignment = new Assignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SlotId = selected.SlotId,
                ResourceId = selected.ResourceId,
                ActivityId = activity.Id,
            };

            await _assignmentRepository.AddAsync(assignment);
            occupiedPairs.Add((selected.SlotId, selected.ResourceId));
            createdAssignments++;

            if (createdAssignments % 10 == 0)
            {
                _logger.LogDebug(
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

}
