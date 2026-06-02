using Chronos.Domain.Schedule.Messages;
using Chronos.Tests.Engine.TestFixtures;

namespace Chronos.Tests.Engine.Specification;

[TestFixture]
[Category("Specification")]
[Category("TDD")]
public class BatchStressTests
{
    private EngineIntegrationFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new EngineIntegrationFixture();

    [TearDown]
    public void TearDown() => _fixture.Dispose();

    [Test]
    public async Task TenActivities_threeWeeks_satisfyInvariants_underTimeBudget()
    {
        var builder = SchedulingScenarioBuilder.Create().WithThreeWeekPeriod();
        for (var i = 0; i < 8; i++)
            builder.WithActivity(durationMinutes: 60);

        builder
            .WithSlot("Monday", 9, 10)
            .WithSlot("Monday", 10, 11)
            .WithSlot("Tuesday", 9, 10)
            .WithSlot("Tuesday", 10, 11)
            .WithResource("Room A")
            .WithResource("Room B");

        var scenario = builder.Build();
        _fixture.LoadScenario(scenario);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await _fixture.BatchStrategy.ExecuteAsync(
            new SchedulePeriodRequest(_fixture.PeriodId, _fixture.OrganizationId),
            CancellationToken.None
        );
        sw.Stop();

        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));

        var violations = SchedulingInvariantVerifier.Verify(
            _fixture.AssignmentRepository.Assignments,
            scenario.Activities,
            _fixture.SlotsById,
            scenario.PeriodFrom,
            scenario.PeriodTo,
            result.UnscheduledActivityIds
        );

        violations.Should().BeEmpty(string.Join("\n", violations));
    }

    [Test]
    public async Task TwoTeachers_sixActivities_noTeacherDoubleBooking()
    {
        var teacher1 = Guid.NewGuid();
        var teacher2 = Guid.NewGuid();
        var builder = SchedulingScenarioBuilder.Create().WithThreeWeekPeriod();

        for (var i = 0; i < 3; i++)
            builder.WithActivity(teacherId: teacher1);
        for (var i = 0; i < 3; i++)
            builder.WithActivity(teacherId: teacher2);

        var scenario = builder
            .WithSlot("Monday", 9, 10)
            .WithSlot("Monday", 10, 11)
            .WithSlot("Tuesday", 9, 10)
            .WithResource("Room A")
            .Build();

        _fixture.LoadScenario(scenario);
        await _fixture.BatchStrategy.ExecuteAsync(
            new SchedulePeriodRequest(_fixture.PeriodId, _fixture.OrganizationId),
            CancellationToken.None
        );

        var violations = SchedulingInvariantVerifier.Verify(
            _fixture.AssignmentRepository.Assignments,
            scenario.Activities,
            _fixture.SlotsById,
            scenario.PeriodFrom,
            scenario.PeriodTo
        );

        violations.Where(v => v.StartsWith("I4:")).Should().BeEmpty();
    }
}
