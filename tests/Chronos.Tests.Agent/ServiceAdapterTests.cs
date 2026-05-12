using Chronos.MainApi.Agent;
using Chronos.MainApi.Schedule.Services;
using Moq;

namespace Chronos.Tests.Agent;

public class ServiceAdapterTests
{
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _periodId = Guid.NewGuid();

    [Fact]
    public async Task UserConstraintServiceAdapter_DelegatesToInner()
    {
        var inner = new Mock<IUserConstraintService>();
        var expectedId = Guid.NewGuid();
        inner.Setup(s => s.CreateUserConstraintAsync(_orgId, _userId, _periodId, "forbidden_timerange", "Friday 09:00 - 17:00", null))
            .ReturnsAsync(expectedId);

        var adapter = new UserConstraintServiceAdapter(inner.Object);

        var result = await adapter.CreateUserConstraintAsync(_orgId, _userId, _periodId, "forbidden_timerange", "Friday 09:00 - 17:00");

        Assert.Equal(expectedId, result);
        inner.Verify(s => s.CreateUserConstraintAsync(_orgId, _userId, _periodId, "forbidden_timerange", "Friday 09:00 - 17:00", null), Times.Once);
    }

    [Fact]
    public async Task UserConstraintServiceAdapter_PassesWeekNum()
    {
        var inner = new Mock<IUserConstraintService>();
        inner.Setup(s => s.CreateUserConstraintAsync(_orgId, _userId, _periodId, "forbidden_timerange", "Monday 09:00 - 17:00", 3))
            .ReturnsAsync(Guid.NewGuid());

        var adapter = new UserConstraintServiceAdapter(inner.Object);

        await adapter.CreateUserConstraintAsync(_orgId, _userId, _periodId, "forbidden_timerange", "Monday 09:00 - 17:00", 3);

        inner.Verify(s => s.CreateUserConstraintAsync(_orgId, _userId, _periodId, "forbidden_timerange", "Monday 09:00 - 17:00", 3), Times.Once);
    }

    [Fact]
    public async Task UserPreferenceServiceAdapter_DelegatesToInner()
    {
        var inner = new Mock<IUserPreferenceService>();
        var expectedId = Guid.NewGuid();
        inner.Setup(s => s.CreateUserPreferenceAsync(_orgId, _userId, _periodId, "preferred_weekday", "Monday"))
            .ReturnsAsync(expectedId);

        var adapter = new UserPreferenceServiceAdapter(inner.Object);

        var result = await adapter.CreateUserPreferenceAsync(_orgId, _userId, _periodId, "preferred_weekday", "Monday");

        Assert.Equal(expectedId, result);
        inner.Verify(s => s.CreateUserPreferenceAsync(_orgId, _userId, _periodId, "preferred_weekday", "Monday"), Times.Once);
    }
}
