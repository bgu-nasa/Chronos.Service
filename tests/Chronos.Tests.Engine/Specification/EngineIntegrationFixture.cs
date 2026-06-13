using Chronos.Data.Context;
using Chronos.Domain.Schedule;
using Chronos.Data.Repositories.Resources;
using Chronos.Data.Repositories.Schedule;
using Chronos.Engine.Constraints;
using Chronos.Engine.Constraints.Evaluation;
using Chronos.Engine.Constraints.Evaluation.Validators;
using Chronos.Engine.Matching;
using Chronos.Tests.Engine.TestFixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Chronos.Tests.Engine.Specification;

/// <summary>
/// Wires the batch/online engine with real constraint evaluation and in-memory assignments.
/// </summary>
public sealed class EngineIntegrationFixture : IDisposable
{
    public Guid OrganizationId { get; private set; }
    public Guid PeriodId { get; private set; }
    public SchedulingScenario Scenario { get; private set; } = null!;

    public AppDbContext DbContext { get; private set; } = null!;
    public FakeAssignmentRepository AssignmentRepository { get; private set; } = null!;
    public RankingAlgorithmStrategy BatchStrategy { get; private set; } = null!;
    public OnlineMatchingStrategy OnlineStrategy { get; private set; } = null!;

    public IActivityRepository ActivityRepository { get; private set; } = null!;
    public ISlotRepository SlotRepository { get; private set; } = null!;
    public IResourceRepository ResourceRepository { get; private set; } = null!;
    public IActivityConstraintRepository ActivityConstraintRepository { get; private set; } = null!;
    public IUserConstraintRepository UserConstraintRepository { get; private set; } = null!;
    public IUserPreferenceRepository UserPreferenceRepository { get; private set; } = null!;

    public void LoadScenario(SchedulingScenario scenario)
    {
        DisposeDbContext();
        Scenario = scenario;
        OrganizationId = scenario.OrganizationId;
        PeriodId = scenario.PeriodId;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        DbContext = new AppDbContext(options, null);
        SchedulingScenarioApplicator.ApplyToDbContext(scenario, DbContext);
        DbContext.SaveChanges();

        AssignmentRepository = new FakeAssignmentRepository();
        AssignmentRepository.SetSchedulingPeriodSlotIds(scenario.Slots.Select(s => s.Id));
        if (scenario.SeedAssignments.Count > 0)
            AssignmentRepository.Seed(scenario.SeedAssignments);

        ActivityRepository = Substitute.For<IActivityRepository>();
        SlotRepository = Substitute.For<ISlotRepository>();
        ResourceRepository = Substitute.For<IResourceRepository>();
        ActivityConstraintRepository = Substitute.For<IActivityConstraintRepository>();
        UserConstraintRepository = Substitute.For<IUserConstraintRepository>();
        UserPreferenceRepository = Substitute.For<IUserPreferenceRepository>();

        SlotRepository.GetBySchedulingPeriodIdAsync(PeriodId).Returns(scenario.Slots);
        ResourceRepository.GetAllAsync().Returns(scenario.Resources);
        UserPreferenceRepository.GetByUserIdAsync(Arg.Any<Guid>()).Returns([]);
        UserPreferenceRepository.GetByUserPeriodAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(scenario.UserPreferences);

        foreach (var activity in scenario.Activities)
        {
            var constraints = scenario.ActivityConstraints
                .Where(c => c.ActivityId == activity.Id)
                .ToList();
            ActivityConstraintRepository.GetByActivityIdAsync(activity.Id).Returns(constraints);

            if (activity.AssignedUserId != Guid.Empty)
            {
                var userConstraints = scenario.UserConstraints
                    .Where(c => c.UserId == activity.AssignedUserId)
                    .ToList();
                UserConstraintRepository
                    .GetByUserPeriodAsync(activity.AssignedUserId, PeriodId)
                    .Returns(userConstraints);
            }
        }

        var constraintProcessor = new ActivityConstraintProcessor(
            ActivityConstraintRepository,
            UserConstraintRepository,
            SlotRepository,
            Enumerable.Empty<IConstraintHandler>(),
            Substitute.For<ILogger<ActivityConstraintProcessor>>()
        );

        var constraintEvaluator = CreateProductionConstraintEvaluator(ActivityConstraintRepository);
        var ranker = new PreferenceWeightedRanker(
            UserPreferenceRepository,
            Substitute.For<ILogger<PreferenceWeightedRanker>>()
        );

        BatchStrategy = new RankingAlgorithmStrategy(
            ActivityRepository,
            SlotRepository,
            ResourceRepository,
            AssignmentRepository,
            constraintProcessor,
            constraintEvaluator,
            ranker,
            DbContext,
            Substitute.For<ILogger<RankingAlgorithmStrategy>>()
        );

        OnlineStrategy = new OnlineMatchingStrategy(
            AssignmentRepository,
            constraintProcessor,
            constraintEvaluator,
            ranker,
            DbContext,
            Substitute.For<ILogger<OnlineMatchingStrategy>>()
        );
    }

    public static ConstraintEvaluator CreateProductionConstraintEvaluator(
        IActivityConstraintRepository? constraintRepository = null
    )
    {
        var resourceTypeRepository = Substitute.For<IResourceTypeRepository>();
        constraintRepository ??= Substitute.For<IActivityConstraintRepository>();

        var validatorServices = new ServiceCollection();
        validatorServices.AddSingleton(resourceTypeRepository);
        var validatorProvider = validatorServices.BuildServiceProvider();
        var validatorScopeFactory = Substitute.For<IServiceScopeFactory>();
        validatorScopeFactory.CreateScope().Returns(_ => validatorProvider.CreateScope());

        var validators = new List<IConstraintValidator>
        {
            new PreferredWeekdaysValidator(Substitute.For<ILogger<PreferredWeekdaysValidator>>()),
            new TimeRangeValidator(Substitute.For<ILogger<TimeRangeValidator>>()),
            new RequiredCapacityValidator(Substitute.For<ILogger<RequiredCapacityValidator>>()),
            new LocationPreferenceValidator(Substitute.For<ILogger<LocationPreferenceValidator>>()),
            new ActivityTypeCompatibilityValidator(
                validatorScopeFactory,
                Substitute.For<ILogger<ActivityTypeCompatibilityValidator>>()
            ),
            new ForbiddenTimeRangeValidator(Substitute.For<ILogger<ForbiddenTimeRangeValidator>>()),
            new PreferredTimeRangeValidator(Substitute.For<ILogger<PreferredTimeRangeValidator>>()),
        };

        var evaluatorServices = new ServiceCollection();
        evaluatorServices.AddSingleton(constraintRepository);
        foreach (var v in validators)
            evaluatorServices.AddSingleton(v);

        var evaluatorProvider = evaluatorServices.BuildServiceProvider();
        var evaluatorScopeFactory = Substitute.For<IServiceScopeFactory>();
        evaluatorScopeFactory.CreateScope().Returns(_ => evaluatorProvider.CreateScope());

        return new ConstraintEvaluator(
            evaluatorScopeFactory,
            Substitute.For<ILogger<ConstraintEvaluator>>()
        );
    }

    public IReadOnlyDictionary<Guid, Slot> SlotsById =>
        Scenario.Slots.ToDictionary(s => s.Id);

    public void Dispose() => DisposeDbContext();

    private void DisposeDbContext()
    {
        DbContext?.Dispose();
    }
}
