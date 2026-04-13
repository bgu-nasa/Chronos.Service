using Chronos.Domain.Schedule.Messages;
using Chronos.Engine.Matching;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Chronos.Tests.Engine.Matching;

[TestFixture]
[Category("Unit")]
public class MatchingOrchestratorTests
{
    private ILogger<MatchingOrchestrator> _logger;

    [SetUp]
    public void SetUp()
    {
        _logger = Substitute.For<ILogger<MatchingOrchestrator>>();
    }

    [Test]
    public void GivenNoStrategyForMode_WhenExecute_ThenThrowsInvalidOperation()
    {
        var orchestrator = new MatchingOrchestrator([], _logger);

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.ExecuteAsync(new object(), SchedulingMode.Online, CancellationToken.None));
    }

    [Test]
    public async Task GivenOnlineStrategy_WhenExecuteOnline_ThenDelegatesToIt()
    {
        var expectedResult = new SchedulingResult(Guid.NewGuid(), true, 5, 0, [], null);
        var onlineStrategy = Substitute.For<IMatchingStrategy>();
        onlineStrategy.Mode.Returns(SchedulingMode.Online);
        onlineStrategy.ExecuteAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var orchestrator = new MatchingOrchestrator([onlineStrategy], _logger);

        var result = await orchestrator.ExecuteAsync("request", SchedulingMode.Online, CancellationToken.None);

        Assert.That(result, Is.EqualTo(expectedResult));
        await onlineStrategy.Received(1).ExecuteAsync("request", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GivenBothStrategies_WhenExecuteBatch_ThenSelectsBatchStrategy()
    {
        var batchResult = new SchedulingResult(Guid.NewGuid(), true, 10, 0, [], null);

        var onlineStrategy = Substitute.For<IMatchingStrategy>();
        onlineStrategy.Mode.Returns(SchedulingMode.Online);

        var batchStrategy = Substitute.For<IMatchingStrategy>();
        batchStrategy.Mode.Returns(SchedulingMode.Batch);
        batchStrategy.ExecuteAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(batchResult);

        var orchestrator = new MatchingOrchestrator([onlineStrategy, batchStrategy], _logger);

        var result = await orchestrator.ExecuteAsync("batch-req", SchedulingMode.Batch, CancellationToken.None);

        Assert.That(result, Is.EqualTo(batchResult));
        await onlineStrategy.DidNotReceive().ExecuteAsync(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }
}
