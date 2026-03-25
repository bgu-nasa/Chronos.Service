using System.Diagnostics;
using Chronos.Data.Repositories.Resources;
using Chronos.Data.Repositories.Schedule;
using Chronos.Engine.Constraints.Evaluation;
using Chronos.Engine.Constraints.Evaluation.Validators;
using Chronos.Tests.Engine.TestFixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Chronos.Tests.Engine.Performance;

[TestFixture]
[Category("Performance")]
public class ConstraintEvaluatorPerformanceTests
{
    private ConstraintEvaluator _evaluator = null!;
    private IActivityConstraintRepository _constraintRepository = null!;
    private IResourceTypeRepository _resourceTypeRepository = null!;
    private IServiceScopeFactory _serviceScopeFactory = null!;

    [SetUp]
    public void SetUp()
    {
        _constraintRepository = Substitute.For<IActivityConstraintRepository>();
        _resourceTypeRepository = Substitute.For<IResourceTypeRepository>();

        // Create service scope factory for validators that need it
        var validatorServiceCollection = new ServiceCollection();
        validatorServiceCollection.AddSingleton(_resourceTypeRepository);
        var validatorServiceProvider = validatorServiceCollection.BuildServiceProvider();
        var validatorServiceScopeFactory = Substitute.For<IServiceScopeFactory>();
        validatorServiceScopeFactory.CreateScope().Returns(callInfo =>
        {
            return validatorServiceProvider.CreateScope();
        });

        var validators = new List<IConstraintValidator>
        {
            new PreferredWeekdaysValidator(Substitute.For<ILogger<PreferredWeekdaysValidator>>()),
            new TimeRangeValidator(Substitute.For<ILogger<TimeRangeValidator>>()),
            new RequiredCapacityValidator(Substitute.For<ILogger<RequiredCapacityValidator>>()),
            new LocationPreferenceValidator(Substitute.For<ILogger<LocationPreferenceValidator>>()),
            new ActivityTypeCompatibilityValidator(
                validatorServiceScopeFactory,
                Substitute.For<ILogger<ActivityTypeCompatibilityValidator>>()
            ),
        };

        // Create a service provider and scope factory for the evaluator
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(_constraintRepository);
        foreach (var validator in validators)
        {
            serviceCollection.AddSingleton(validator);
        }
        var serviceProvider = serviceCollection.BuildServiceProvider();
        _serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceScopeFactory.CreateScope().Returns(callInfo =>
        {
            return serviceProvider.CreateScope();
        });

        _evaluator = new ConstraintEvaluator(
            _serviceScopeFactory,
            Substitute.For<ILogger<ConstraintEvaluator>>()
        );
    }

    [Test]
    public async Task CanAssignAsync_WithSingleConstraint_ShouldCompleteUnder1Ms()
    {
        // Arrange
        var activity = TestDataBuilder.CreateActivity();
        var slot = TestDataBuilder.CreateSlot(weekday: "Monday");
        var resource = TestDataBuilder.CreateResource();

        var constraints = new List<Domain.Schedule.ActivityConstraint>
        {
            TestDataBuilder.CreateConstraint(
                activityId: activity.Id,
                key: "preferred_weekdays",
                value: "Monday,Wednesday,Friday"
            ),
        };

        _constraintRepository.GetByActivityIdAsync(activity.Id).Returns(constraints);

        // Warm up
        await _evaluator.CanAssignAsync(activity, slot, resource);

        // Act
        var stopwatch = Stopwatch.StartNew();
        await _evaluator.CanAssignAsync(activity, slot, resource);
        stopwatch.Stop();

        // Assert
        stopwatch
            .ElapsedMilliseconds.Should()
            .BeLessThan(1, $"Evaluation took {stopwatch.ElapsedMilliseconds}ms, expected <1ms");
    }

    [Test]
    public async Task CanAssignAsync_WithFiveConstraints_ShouldCompleteUnder1Ms()
    {
        // Arrange
        var resourceTypeId = Guid.NewGuid();
        var resourceType = TestDataBuilder.CreateResourceType(
            id: resourceTypeId,
            type: "Lecture Hall"
        );
        _resourceTypeRepository.GetByIdAsync(resourceTypeId).Returns(resourceType);

        var activity = TestDataBuilder.CreateActivity(expectedStudents: 30);
        var slot = TestDataBuilder.CreateSlot(
            weekday: "Monday",
            fromTime: new TimeSpan(9, 0, 0),
            toTime: new TimeSpan(10, 0, 0)
        );
        var resource = TestDataBuilder.CreateResource(
            resourceTypeId: resourceTypeId,
            location: "Building A",
            capacity: 50
        );

        var constraints = new List<Domain.Schedule.ActivityConstraint>
        {
            TestDataBuilder.CreateConstraint(
                activityId: activity.Id,
                key: "preferred_weekdays",
                value: "Monday,Wednesday,Friday"
            ),
            TestDataBuilder.CreateConstraint(
                activityId: activity.Id,
                key: "time_range",
                value: "{\"start\": \"08:00\", \"end\": \"17:00\"}"
            ),
            TestDataBuilder.CreateConstraint(
                activityId: activity.Id,
                key: "required_capacity",
                value: "{\"min\": 30, \"max\": 100}"
            ),
            TestDataBuilder.CreateConstraint(
                activityId: activity.Id,
                key: "location_preference",
                value: "Building A,Building B"
            ),
            TestDataBuilder.CreateConstraint(
                activityId: activity.Id,
                key: "compatible_resource_types",
                value: "Lecture Hall,Seminar Room"
            ),
        };

        _constraintRepository.GetByActivityIdAsync(activity.Id).Returns(constraints);

        // Warm up
        await _evaluator.CanAssignAsync(activity, slot, resource);

        // Act
        var stopwatch = Stopwatch.StartNew();
        await _evaluator.CanAssignAsync(activity, slot, resource);
        stopwatch.Stop();

        // Assert
        stopwatch
            .ElapsedMilliseconds.Should()
            .BeLessThan(1, $"Evaluation took {stopwatch.ElapsedMilliseconds}ms, expected <1ms");
    }

    [Test]
    public async Task GetViolationsAsync_WithMultipleConstraints_ShouldCompleteUnder1Ms()
    {
        // Arrange
        var activity = TestDataBuilder.CreateActivity(expectedStudents: 60);
        var slot = TestDataBuilder.CreateSlot(
            weekday: "Tuesday",
            fromTime: new TimeSpan(7, 0, 0),
            toTime: new TimeSpan(9, 0, 0)
        );
        var resource = TestDataBuilder.CreateResource(location: "Building D", capacity: 50);

        var constraints = new List<Domain.Schedule.ActivityConstraint>
        {
            TestDataBuilder.CreateConstraint(
                activityId: activity.Id,
                key: "preferred_weekdays",
                value: "Monday,Wednesday,Friday"
            ),
            TestDataBuilder.CreateConstraint(
                activityId: activity.Id,
                key: "time_range",
                value: "{\"start\": \"08:00\", \"end\": \"17:00\"}"
            ),
            TestDataBuilder.CreateConstraint(
                activityId: activity.Id,
                key: "required_capacity",
                value: "{\"min\": 30}"
            ),
            TestDataBuilder.CreateConstraint(
                activityId: activity.Id,
                key: "location_preference",
                value: "Building A,Building B"
            ),
        };

        _constraintRepository.GetByActivityIdAsync(activity.Id).Returns(constraints);

        // Warm up
        await _evaluator.GetViolationsAsync(activity, slot, resource);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var violations = await _evaluator.GetViolationsAsync(activity, slot, resource);
        stopwatch.Stop();

        // Assert
        stopwatch
            .ElapsedMilliseconds.Should()
            .BeLessThan(1, $"Evaluation took {stopwatch.ElapsedMilliseconds}ms, expected <1ms");
    }

    [Test]
    public async Task CanAssignAsync_BenchmarkAverageOver100Iterations()
    {
        // Arrange
        var activity = TestDataBuilder.CreateActivity();
        var slot = TestDataBuilder.CreateSlot(weekday: "Monday");
        var resource = TestDataBuilder.CreateResource(capacity: 50);

        var constraints = new List<Domain.Schedule.ActivityConstraint>
        {
            TestDataBuilder.CreateConstraint(
                activityId: activity.Id,
                key: "preferred_weekdays",
                value: "Monday,Wednesday,Friday"
            ),
            TestDataBuilder.CreateConstraint(
                activityId: activity.Id,
                key: "required_capacity",
                value: "{\"min\": 30}"
            ),
        };

        _constraintRepository.GetByActivityIdAsync(activity.Id).Returns(constraints);

        // Warm up
        for (int i = 0; i < 10; i++)
        {
            await _evaluator.CanAssignAsync(activity, slot, resource);
        }

        // Act - Benchmark
        var iterations = 100;
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            await _evaluator.CanAssignAsync(activity, slot, resource);
        }

        stopwatch.Stop();

        var averageMs = stopwatch.Elapsed.TotalMilliseconds / iterations;

        // Assert
        averageMs
            .Should()
            .BeLessThan(1.0, $"Average evaluation took {averageMs:F3}ms, expected <1ms");
    }
}
