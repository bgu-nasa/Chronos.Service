using Chronos.Data.Repositories.Resources;
using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Constraints;
using Chronos.Engine.Constraints.Evaluation;
using Chronos.Engine.Constraints.Evaluation.Validators;
using Chronos.Tests.Engine.TestFixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Chronos.Tests.Engine.Integration;

[TestFixture]
[Category("Integration")]
public class ConstraintEvaluatorIntegrationTests
{
    private ConstraintEvaluator _evaluator = null!;
    private IActivityConstraintRepository _constraintRepository = null!;
    private IResourceTypeRepository _resourceTypeRepository = null!;
    private ILogger<ConstraintEvaluator> _evaluatorLogger = null!;
    private List<IConstraintValidator> _validators = null!;
    private IServiceScopeFactory _serviceScopeFactory = null!;

    [SetUp]
    public void SetUp()
    {
        _constraintRepository = Substitute.For<IActivityConstraintRepository>();
        _resourceTypeRepository = Substitute.For<IResourceTypeRepository>();
        _evaluatorLogger = Substitute.For<ILogger<ConstraintEvaluator>>();

        // Create service scope factory for validators that need it
        var validatorServiceCollection = new ServiceCollection();
        validatorServiceCollection.AddSingleton(_resourceTypeRepository);
        var validatorServiceProvider = validatorServiceCollection.BuildServiceProvider();
        var validatorServiceScopeFactory = Substitute.For<IServiceScopeFactory>();
        validatorServiceScopeFactory.CreateScope().Returns(callInfo =>
        {
            return validatorServiceProvider.CreateScope();
        });

        // Register all validators
        _validators = new List<IConstraintValidator>
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
        foreach (var validator in _validators)
        {
            serviceCollection.AddSingleton(validator);
        }
        var serviceProvider = serviceCollection.BuildServiceProvider();
        _serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceScopeFactory.CreateScope().Returns(callInfo =>
        {
            return serviceProvider.CreateScope();
        });

        _evaluator = new ConstraintEvaluator(_serviceScopeFactory, _evaluatorLogger);
    }

    [Test]
    public async Task CanAssignAsync_WhenExpectedStudentsExceedCapacity_withoutRequiredCapacityConstraint_ShouldReturnFalse()
    {
        var activity = TestDataBuilder.CreateActivity(expectedStudents: 30);
        var slot = TestDataBuilder.CreateSlot();
        var resource = TestDataBuilder.CreateResource(capacity: 15);

        _constraintRepository
            .GetByActivityIdAsync(activity.Id)
            .Returns(new List<Domain.Schedule.ActivityConstraint>());

        var canAssign = await _evaluator.CanAssignAsync(activity, slot, resource);

        canAssign.Should().BeFalse();
    }

    [Test]
    public async Task CanAssignAsync_WithNoConstraints_ShouldReturnTrue()
    {
        // Arrange
        var activity = TestDataBuilder.CreateActivity();
        var slot = TestDataBuilder.CreateSlot();
        var resource = TestDataBuilder.CreateResource();

        _constraintRepository
            .GetByActivityIdAsync(activity.Id)
            .Returns(new List<Domain.Schedule.ActivityConstraint>());

        // Act
        var result = await _evaluator.CanAssignAsync(activity, slot, resource);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task CanAssignAsync_WithOnlySoftConstraintViolations_ShouldReturnTrue()
    {
        // Arrange
        var activity = TestDataBuilder.CreateActivity();
        var slot = TestDataBuilder.CreateSlot(weekday: "Tuesday");
        var resource = TestDataBuilder.CreateResource(location: "Building D");

        var constraints = new List<Domain.Schedule.ActivityConstraint>
        {
            TestDataBuilder.CreateConstraint(
                activityId: activity.Id,
                key: "preferred_weekdays",
                value: "Monday,Wednesday,Friday"
            ),
            TestDataBuilder.CreateConstraint(
                activityId: activity.Id,
                key: "location_preference",
                value: "Building A,Building B"
            ),
        };

        _constraintRepository.GetByActivityIdAsync(activity.Id).Returns(constraints);

        // Act
        var result = await _evaluator.CanAssignAsync(activity, slot, resource);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task CanAssignAsync_WithHardConstraintViolation_ShouldReturnFalse()
    {
        // Arrange
        var activity = TestDataBuilder.CreateActivity();
        var slot = TestDataBuilder.CreateSlot(
            fromTime: new TimeSpan(7, 0, 0),
            toTime: new TimeSpan(9, 0, 0)
        );
        var resource = TestDataBuilder.CreateResource(capacity: 50);

        var constraints = new List<Domain.Schedule.ActivityConstraint>
        {
            TestDataBuilder.CreateConstraint(
                activityId: activity.Id,
                key: "time_range",
                value: """{"start": "08:00", "end": "17:00"}"""
            ),
        };

        _constraintRepository.GetByActivityIdAsync(activity.Id).Returns(constraints);

        // Act
        var result = await _evaluator.CanAssignAsync(activity, slot, resource);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task GetViolationsAsync_WithMultipleConstraints_ShouldReturnAllViolations()
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
                value: """{"start": "08:00", "end": "17:00"}"""
            ),
            TestDataBuilder.CreateConstraint(
                activityId: activity.Id,
                key: "required_capacity",
                value: """{"min": 30}"""
            ),
            TestDataBuilder.CreateConstraint(
                activityId: activity.Id,
                key: "location_preference",
                value: "Building A,Building B"
            ),
        };

        _constraintRepository.GetByActivityIdAsync(activity.Id).Returns(constraints);

        // Act
        var violations = (await _evaluator.GetViolationsAsync(activity, slot, resource)).ToList();

        // Assert
        violations.Should().HaveCount(4);
        violations
            .Should()
            .Contain(v =>
                v.ConstraintKey == "preferred_weekdays" && v.ViolationType == ViolationType.Soft
            );
        violations
            .Should()
            .Contain(v => v.ConstraintKey == "time_range" && v.ViolationType == ViolationType.Hard);
        violations
            .Should()
            .Contain(v =>
                v.ConstraintKey == "required_capacity" && v.ViolationType == ViolationType.Hard
            );
        violations
            .Should()
            .Contain(v =>
                v.ConstraintKey == "location_preference" && v.ViolationType == ViolationType.Soft
            );
    }

    [Test]
    public async Task GetViolationsAsync_WithUnknownConstraintKey_ShouldSkipConstraint()
    {
        // Arrange
        var activity = TestDataBuilder.CreateActivity();
        var slot = TestDataBuilder.CreateSlot();
        var resource = TestDataBuilder.CreateResource();

        var constraints = new List<Domain.Schedule.ActivityConstraint>
        {
            TestDataBuilder.CreateConstraint(
                activityId: activity.Id,
                key: "unknown_constraint_type",
                value: "some_value"
            ),
        };

        _constraintRepository.GetByActivityIdAsync(activity.Id).Returns(constraints);

        // Act
        var violations = await _evaluator.GetViolationsAsync(activity, slot, resource);

        // Assert
        violations.Should().BeEmpty();
    }

    [Test]
    public async Task GetViolationsAsync_WithAllConstraintsSatisfied_ShouldReturnEmpty()
    {
        // Arrange
        var resourceTypeId = Guid.NewGuid();
        var resourceType = TestDataBuilder.CreateResourceType(
            id: resourceTypeId,
            type: "Lecture Hall"
        );
        _resourceTypeRepository.GetByIdAsync(resourceTypeId).Returns(resourceType);

        var activity = TestDataBuilder.CreateActivity(
            activityType: "Lecture",
            expectedStudents: 30
        );
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
                value: """{"start": "08:00", "end": "17:00"}"""
            ),
            TestDataBuilder.CreateConstraint(
                activityId: activity.Id,
                key: "required_capacity",
                value: """{"min": 30, "max": 100}"""
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

        // Act
        var violations = await _evaluator.GetViolationsAsync(activity, slot, resource);

        // Assert
        violations.Should().BeEmpty();
    }
}
