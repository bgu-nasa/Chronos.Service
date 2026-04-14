using Chronos.Agent.Domain;
using Chronos.Agent.Submission;
using Chronos.Domain.Schedule;
using Moq;

namespace Chronos.Tests.Agent;

public class ChronosSubmitterTests
{
    [Fact]
    public async Task SubmitAsync_WritesAllConstraints()
    {
        var mockSubmitter = new Mock<IAgentSubmitter>();
        mockSubmitter
            .Setup(s => s.SubmitAsync(It.IsAny<ConstraintProposal>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var constraints = new List<UserConstraint>
        {
            new() { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), UserId = Guid.NewGuid(),
                     SchedulingPeriodId = Guid.NewGuid(), Key = "avoid_weekday", Value = "Friday" }
        };
        var preferences = new List<UserPreference>
        {
            new() { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), UserId = Guid.NewGuid(),
                     SchedulingPeriodId = Guid.NewGuid(), Key = "preferred_weekday", Value = "Monday" }
        };
        var proposal = new ConstraintProposal(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            constraints, preferences);

        await mockSubmitter.Object.SubmitAsync(proposal);

        mockSubmitter.Verify(
            s => s.SubmitAsync(It.Is<ConstraintProposal>(p =>
                p.Constraints.Count == 1 && p.Preferences.Count == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

/// <summary>
/// Tests for the concrete ServiceBackedSubmitter that delegates to existing Chronos services.
/// </summary>
public class ServiceBackedSubmitterTests
{
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _periodId = Guid.NewGuid();

    [Fact]
    public async Task SubmitAsync_CallsConstraintService_ForEachConstraint()
    {
        var constraintSvc = new Mock<IAgentConstraintService>();
        var preferenceSvc = new Mock<IAgentPreferenceService>();

        var submitter = new ServiceBackedSubmitter(constraintSvc.Object, preferenceSvc.Object);

        var constraints = new List<UserConstraint>
        {
            MakeConstraint("avoid_weekday", "Friday"),
            MakeConstraint("unavailable_day", "Saturday")
        };
        var proposal = new ConstraintProposal(_userId, _orgId, _periodId, constraints, new List<UserPreference>());

        await submitter.SubmitAsync(proposal);

        constraintSvc.Verify(
            s => s.CreateUserConstraintAsync(_orgId, _userId, _periodId, "avoid_weekday", "Friday", null),
            Times.Once);
        constraintSvc.Verify(
            s => s.CreateUserConstraintAsync(_orgId, _userId, _periodId, "unavailable_day", "Saturday", null),
            Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_CallsPreferenceService_ForEachPreference()
    {
        var constraintSvc = new Mock<IAgentConstraintService>();
        var preferenceSvc = new Mock<IAgentPreferenceService>();

        var submitter = new ServiceBackedSubmitter(constraintSvc.Object, preferenceSvc.Object);

        var preferences = new List<UserPreference>
        {
            MakePreference("preferred_weekday", "Monday"),
            MakePreference("preferred_time_morning", "true")
        };
        var proposal = new ConstraintProposal(_userId, _orgId, _periodId, new List<UserConstraint>(), preferences);

        await submitter.SubmitAsync(proposal);

        preferenceSvc.Verify(
            s => s.CreateUserPreferenceAsync(_orgId, _userId, _periodId, "preferred_weekday", "Monday"),
            Times.Once);
        preferenceSvc.Verify(
            s => s.CreateUserPreferenceAsync(_orgId, _userId, _periodId, "preferred_time_morning", "true"),
            Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_EmptyProposal_DoesNotCallServices()
    {
        var constraintSvc = new Mock<IAgentConstraintService>();
        var preferenceSvc = new Mock<IAgentPreferenceService>();

        var submitter = new ServiceBackedSubmitter(constraintSvc.Object, preferenceSvc.Object);
        var proposal = new ConstraintProposal(_userId, _orgId, _periodId,
            new List<UserConstraint>(), new List<UserPreference>());

        await submitter.SubmitAsync(proposal);

        constraintSvc.Verify(
            s => s.CreateUserConstraintAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()),
            Times.Never);
        preferenceSvc.Verify(
            s => s.CreateUserPreferenceAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    private UserConstraint MakeConstraint(string key, string value) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = _orgId,
        UserId = _userId,
        SchedulingPeriodId = _periodId,
        Key = key,
        Value = value
    };

    private UserPreference MakePreference(string key, string value) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = _orgId,
        UserId = _userId,
        SchedulingPeriodId = _periodId,
        Key = key,
        Value = value
    };
}
