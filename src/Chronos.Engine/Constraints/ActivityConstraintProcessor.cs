using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Schedule;

namespace Chronos.Engine.Constraints;

/// <summary>
/// Orchestrates constraint processing using registered handlers
/// </summary>
public class ActivityConstraintProcessor(
    IActivityConstraintRepository constraintRepository,
    IUserConstraintRepository userConstraintRepository,
    ISlotRepository slotRepository,
    IEnumerable<IConstraintHandler> handlers,
    ILogger<ActivityConstraintProcessor> logger
) : IConstraintProcessor
{
    private readonly IActivityConstraintRepository _constraintRepository = constraintRepository;
    private readonly IUserConstraintRepository _userConstraintRepository = userConstraintRepository;
    private readonly ISlotRepository _slotRepository = slotRepository;
    private readonly IEnumerable<IConstraintHandler> _handlers = handlers;
    private readonly ILogger<ActivityConstraintProcessor> _logger = logger;

    public async Task<HashSet<Guid>> GetExcludedSlotIdsAsync(
        Guid activityId,
        Guid organizationId,
        Guid? userId = null,
        Guid? schedulingPeriodId = null
    )
    {
        _logger.LogDebug(
            "Getting excluded slots for Activity {ActivityId} in Organization {OrganizationId}" +
            (userId.HasValue ? " for User {UserId}" : ""),
            activityId,
            organizationId,
            userId
        );

        var excludedSlots = new HashSet<Guid>();

        // Load all constraints for this activity
        var activityConstraints = await _constraintRepository.GetByActivityIdAsync(activityId);

        _logger.LogDebug(
            "Processing {ConstraintCount} activity constraints for Activity {ActivityId}",
            activityConstraints.Count,
            activityId
        );

        // Process activity constraints
        foreach (var constraint in activityConstraints)
        {
            var excludedByThisConstraint = await ProcessConstraintAsync(
                constraint,
                organizationId,
                activityId,
                schedulingPeriodId
            );

            excludedSlots.UnionWith(excludedByThisConstraint);
        }

        // Load and process user constraints if userId and schedulingPeriodId are provided
        if (userId.HasValue && schedulingPeriodId.HasValue)
        {
            var userConstraints = await _userConstraintRepository.GetByUserPeriodAsync(
                userId.Value,
                schedulingPeriodId.Value
            );

            // Filter to only constraints for this organization
            var orgUserConstraints = userConstraints
                .Where(uc => uc.OrganizationId == organizationId)
                .ToList();

            _logger.LogInformation(
                "Processing {ConstraintCount} user constraints for User {UserId} in SchedulingPeriod {SchedulingPeriodId}",
                orgUserConstraints.Count,
                userId.Value,
                schedulingPeriodId.Value
            );

            foreach (var uc in orgUserConstraints)
            {
                _logger.LogDebug(
                    "User constraint: Key={Key}, Value={Value}",
                    uc.Key,
                    uc.Value
                );
            }

            foreach (var userConstraint in orgUserConstraints)
            {
                // Convert UserConstraint to ActivityConstraint format for processing
                var activityConstraint = new ActivityConstraint
                {
                    Id = userConstraint.Id,
                    OrganizationId = userConstraint.OrganizationId,
                    ActivityId = activityId, // Use activityId for context
                    Key = userConstraint.Key,
                    Value = userConstraint.Value
                };

                var excludedByThisConstraint = await ProcessConstraintAsync(
                    activityConstraint,
                    organizationId,
                    activityId,
                    schedulingPeriodId
                );

                excludedSlots.UnionWith(excludedByThisConstraint);
            }
        }

        _logger.LogInformation(
            "Total {ExcludedCount} slots excluded for Activity {ActivityId}",
            excludedSlots.Count,
            activityId
        );

        return excludedSlots;
    }

    private async Task<HashSet<Guid>> ProcessConstraintAsync(
        ActivityConstraint constraint,
        Guid organizationId,
        Guid activityId,
        Guid? schedulingPeriodId = null
    )
    {
        // Try to find a handler for this constraint type
        var handler = _handlers.FirstOrDefault(h => h.ConstraintKey == constraint.Key);

        if (handler != null)
        {
            _logger.LogTrace(
                "Processing constraint {ConstraintKey}={ConstraintValue} with handler {HandlerType}",
                constraint.Key,
                constraint.Value,
                handler.GetType().Name
            );

            var excludedByThisConstraint = await handler.ProcessConstraintAsync(
                constraint,
                organizationId
            );

            _logger.LogDebug(
                "Constraint {ConstraintKey}={ConstraintValue} excluded {SlotCount} slots",
                constraint.Key,
                constraint.Value,
                excludedByThisConstraint.Count
            );

            return excludedByThisConstraint;
        }

        // If no handler found, try built-in processing for common constraint types
        if (constraint.Key == "forbidden_timerange")
        {
            return await ProcessForbiddenTimeRangeAsync(constraint, organizationId, schedulingPeriodId);
        }

        _logger.LogWarning(
            "No handler found for constraint key '{ConstraintKey}' on Activity {ActivityId}. Skipping.",
            constraint.Key,
            activityId
        );

        return new HashSet<Guid>();
    }

    private async Task<HashSet<Guid>> ProcessForbiddenTimeRangeAsync(
        ActivityConstraint constraint,
        Guid organizationId,
        Guid? schedulingPeriodId = null
    )
    {
        if (string.IsNullOrWhiteSpace(constraint.Value))
        {
            _logger.LogWarning(
                "Empty forbidden_timerange constraint. Skipping."
            );
            return new HashSet<Guid>();
        }

        // Parse forbidden time ranges (stored in UTC)
        // Note: Constraints are stored in UTC (converted from user's local time in frontend).
        // Slots are also stored in UTC, so we can compare them directly without conversion.
        _logger.LogInformation(
            "Processing forbidden_timerange constraint. Raw value from database: '{ConstraintValue}'",
            constraint.Value
        );

        var forbiddenRanges = ParseForbiddenRanges(constraint.Value);

        _logger.LogInformation(
            "Parsed {RangeCount} forbidden time ranges from constraint value (UTC): {Value}. UTC Ranges: {Ranges}",
            forbiddenRanges.Count,
            constraint.Value,
            string.Join("; ", forbiddenRanges.Select(r => $"{r.Weekday} {r.StartTime}-{r.EndTime}"))
        );

        if (!forbiddenRanges.Any())
        {
            _logger.LogWarning(
                "No valid forbidden time ranges found: {Value}",
                constraint.Value
            );
            return new HashSet<Guid>();
        }

        // Get slots for the organization (and scheduling period if specified)
        List<Slot> orgSlots;
        if (schedulingPeriodId.HasValue)
        {
            // For user constraints scoped to a scheduling period, only check slots in that period
            orgSlots = await _slotRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId.Value);
            orgSlots = orgSlots.Where(s => s.OrganizationId == organizationId).ToList();
        }
        else
        {
            // For activity constraints (not period-specific), check all slots in the organization
            var allSlots = await _slotRepository.GetAllAsync();
            orgSlots = allSlots.Where(s => s.OrganizationId == organizationId).ToList();
        }

        var excludedSlots = new HashSet<Guid>();

        _logger.LogInformation(
            "Processing {SlotCount} slots against {RangeCount} forbidden ranges (constraint value: {ConstraintValue})",
            orgSlots.Count,
            forbiddenRanges.Count,
            constraint.Value
        );

        foreach (var slot in orgSlots)
        {
            foreach (var forbiddenRange in forbiddenRanges)
            {
                // Normalize both weekdays for comparison
                var slotWeekday = NormalizeWeekday(slot.Weekday);
                var forbiddenWeekday = NormalizeWeekday(forbiddenRange.Weekday);

                _logger.LogTrace(
                    "Checking slot {SlotId}: Weekday={SlotWeekday}, Time={FromTime}-{ToTime} (UTC) against forbidden range: Weekday={ForbiddenWeekday}, Time={ForbiddenStart}-{ForbiddenEnd} (UTC)",
                    slot.Id,
                    slotWeekday,
                    slot.FromTime,
                    slot.ToTime,
                    forbiddenWeekday,
                    forbiddenRange.StartTime,
                    forbiddenRange.EndTime
                );

                // Check if weekday matches (case-insensitive)
                if (!string.Equals(slotWeekday, forbiddenWeekday, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogTrace(
                        "Weekday mismatch: {SlotWeekday} != {ForbiddenWeekday}",
                        slotWeekday,
                        forbiddenWeekday
                    );
                    continue; // Different weekday, no conflict
                }

                // Check if time ranges overlap (both in UTC)
                // Two time ranges overlap if: slotStart < forbiddenEnd AND slotEnd > forbiddenStart
                var overlaps = slot.FromTime < forbiddenRange.EndTime && slot.ToTime > forbiddenRange.StartTime;

                _logger.LogTrace(
                    "Time overlap check for slot {SlotId} (UTC): {FromTime} < {ForbiddenEnd} = {Check1}, {ToTime} > {ForbiddenStart} = {Check2}, Overlaps = {Overlaps}",
                    slot.Id,
                    slot.FromTime,
                    forbiddenRange.EndTime,
                    slot.FromTime < forbiddenRange.EndTime,
                    slot.ToTime,
                    forbiddenRange.StartTime,
                    slot.ToTime > forbiddenRange.StartTime,
                    overlaps
                );

                if (overlaps)
                {
                    _logger.LogInformation(
                        "Excluding slot {SlotId} on {Weekday} ({FromTime}-{ToTime} UTC) due to forbidden range {ForbiddenStart}-{ForbiddenEnd} (UTC)",
                        slot.Id,
                        slot.Weekday,
                        slot.FromTime,
                        slot.ToTime,
                        forbiddenRange.StartTime,
                        forbiddenRange.EndTime
                    );
                    excludedSlots.Add(slot.Id);
                    break; // Slot is excluded, no need to check other ranges
                }
            }
        }

        _logger.LogDebug(
            "forbidden_timerange constraint excluded {SlotCount} slots",
            excludedSlots.Count
        );

        return excludedSlots;
    }

    private List<ForbiddenTimeRange> ParseForbiddenRanges(string value)
    {
        var ranges = new List<ForbiddenTimeRange>();

        // Split by comma or newline
        var entries = value.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Pattern: "Weekday HH:mm - HH:mm" or "Weekday HH:mm-HH:mm"
        var pattern = @"^(\w+)\s+(\d{1,2}:\d{2})\s*-\s*(\d{1,2}:\d{2})$";

        foreach (var entry in entries)
        {
            var trimmedEntry = entry.Trim();
            if (string.IsNullOrWhiteSpace(trimmedEntry))
            {
                continue;
            }

            var match = System.Text.RegularExpressions.Regex.Match(trimmedEntry, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                _logger.LogWarning(
                    "Invalid forbidden_timerange format: '{Entry}'. Expected format: 'Weekday HH:mm - HH:mm'",
                    trimmedEntry
                );
                continue;
            }

            var weekdayRaw = match.Groups[1].Value;
            // Normalize weekday to match database format (capitalized: "Monday", "Tuesday", etc.)
            var weekday = NormalizeWeekday(weekdayRaw);
            var startTimeStr = match.Groups[2].Value;
            var endTimeStr = match.Groups[3].Value;

            if (!TimeSpan.TryParse(startTimeStr, out var startTime) ||
                !TimeSpan.TryParse(endTimeStr, out var endTime))
            {
                _logger.LogWarning(
                    "Invalid time format in forbidden_timerange: '{Entry}'. Use HH:mm format",
                    trimmedEntry
                );
                continue;
            }

            if (startTime >= endTime)
            {
                _logger.LogWarning(
                    "Start time must be before end time in forbidden_timerange: '{Entry}'",
                    trimmedEntry
                );
                continue;
            }

            ranges.Add(new ForbiddenTimeRange
            {
                Weekday = weekday,
                StartTime = startTime,
                EndTime = endTime
            });
        }

        return ranges;
    }

    private static string NormalizeWeekday(string weekday)
    {
        if (string.IsNullOrWhiteSpace(weekday))
        {
            return weekday;
        }

        // Normalize to capitalized format: "Monday", "Tuesday", etc.
        // Handle common variations
        var normalized = weekday.Trim();

        // Capitalize first letter, lowercase the rest
        if (normalized.Length > 0)
        {
            normalized = char.ToUpperInvariant(normalized[0]) +
                        (normalized.Length > 1 ? normalized.Substring(1).ToLowerInvariant() : "");
        }

        return normalized;
    }


    private class ForbiddenTimeRange
    {
        public string Weekday { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }
}
