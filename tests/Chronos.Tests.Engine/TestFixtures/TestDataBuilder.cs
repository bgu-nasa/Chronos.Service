using Chronos.Domain.Resources;
using Chronos.Domain.Schedule;

namespace Chronos.Tests.Engine.TestFixtures;

/// <summary>
/// Provides test data builders for domain objects
/// </summary>
public static class TestDataBuilder
{
    public static Activity CreateActivity(
        Guid? id = null,
        Guid? organizationId = null,
        string activityType = "Lecture",
        int? expectedStudents = null
    )
    {
        return new Activity
        {
            Id = id ?? Guid.NewGuid(),
            OrganizationId = organizationId ?? Guid.NewGuid(),
            SubjectId = Guid.NewGuid(),
            AssignedUserId = Guid.NewGuid(),
            ActivityType = activityType,
            ExpectedStudents = expectedStudents,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public static Slot CreateSlot(
        Guid? id = null,
        Guid? organizationId = null,
        string weekday = "Monday",
        TimeSpan? fromTime = null,
        TimeSpan? toTime = null
    )
    {
        return new Slot
        {
            Id = id ?? Guid.NewGuid(),
            OrganizationId = organizationId ?? Guid.NewGuid(),
            SchedulingPeriodId = Guid.NewGuid(),
            Weekday = weekday,
            FromTime = fromTime ?? new TimeSpan(9, 0, 0),
            ToTime = toTime ?? new TimeSpan(10, 0, 0),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public static Resource CreateResource(
        Guid? id = null,
        Guid? organizationId = null,
        Guid? resourceTypeId = null,
        string location = "Building A",
        string identifier = "Room 101",
        int? capacity = null
    )
    {
        return new Resource
        {
            Id = id ?? Guid.NewGuid(),
            OrganizationId = organizationId ?? Guid.NewGuid(),
            ResourceTypeId = resourceTypeId ?? Guid.NewGuid(),
            Location = location,
            Identifier = identifier,
            Capacity = capacity,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public static ActivityConstraint CreateConstraint(
        Guid? id = null,
        Guid? activityId = null,
        Guid? organizationId = null,
        string key = "test_constraint",
        string value = "test_value"
    )
    {
        return new ActivityConstraint
        {
            Id = id ?? Guid.NewGuid(),
            ActivityId = activityId ?? Guid.NewGuid(),
            OrganizationId = organizationId ?? Guid.NewGuid(),
            Key = key,
            Value = value,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public static ResourceType CreateResourceType(
        Guid? id = null,
        Guid? organizationId = null,
        string type = "Lecture Hall"
    )
    {
        return new ResourceType
        {
            Id = id ?? Guid.NewGuid(),
            OrganizationId = organizationId ?? Guid.NewGuid(),
            Type = type,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public static List<Slot> CreateConsecutiveSlots(
        Guid organizationId,
        Guid schedulingPeriodId,
        string weekday,
        params (int fromHour, int toHour)[] blocks
    )
    {
        var slots = new List<Slot>();
        foreach (var (fromH, toH) in blocks)
        {
            slots.Add(new Slot
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SchedulingPeriodId = schedulingPeriodId,
                Weekday = weekday,
                FromTime = new TimeSpan(fromH, 0, 0),
                ToTime = new TimeSpan(toH, 0, 0),
            });
        }

        return slots;
    }
}
