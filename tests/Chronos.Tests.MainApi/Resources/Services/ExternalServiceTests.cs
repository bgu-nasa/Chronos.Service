using Chronos.Domain.Management;
using Chronos.MainApi.Management.Services;
using Chronos.MainApi.Resources.Services;
using Chronos.MainApi.Schedule.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Chronos.Tests.MainApi.Resources.Services;

[TestFixture]
[Category("Unit")]
public class ExternalDepartmentServiceTests
{
    private IDepartmentService _departmentService;
    private ExternalDepartmentService _sut;

    [SetUp]
    public void SetUp()
    {
        _departmentService = Substitute.For<IDepartmentService>();
        _sut = new ExternalDepartmentService(
            _departmentService,
            Substitute.For<ILogger<ExternalDepartmentService>>());
    }

    [Test]
    public void GivenDepartmentNotFound_WhenValidate_ThenThrows()
    {
        var orgId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        _departmentService.GetDepartmentAsync(orgId, deptId)
            .Returns(Task.FromResult<Department>(null!));

        Assert.ThrowsAsync<Exception>(() =>
            _sut.validateDepartmentAsync(orgId, deptId));
    }

    [Test]
    public void GivenDepartmentExists_WhenValidate_ThenDoesNotThrow()
    {
        var orgId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        _departmentService.GetDepartmentAsync(orgId, deptId)
            .Returns(new Department { Id = deptId, OrganizationId = orgId, Name = "Test" });

        Assert.DoesNotThrowAsync(() =>
            _sut.validateDepartmentAsync(orgId, deptId));
    }
}

[TestFixture]
[Category("Unit")]
public class ExternalSchedulingPeriodServiceTests
{
    private ISchedulingPeriodService _schedulingPeriodService;
    private ExternalSchedulingPeriodService _sut;

    [SetUp]
    public void SetUp()
    {
        _schedulingPeriodService = Substitute.For<ISchedulingPeriodService>();
        _sut = new ExternalSchedulingPeriodService(
            _schedulingPeriodService,
            Substitute.For<ILogger<ExternalSchedulingPeriodService>>());
    }

    [Test]
    public void GivenInvalidPeriod_WhenValidate_ThenPropagatesException()
    {
        var orgId = Guid.NewGuid();
        var spId = Guid.NewGuid();
        _schedulingPeriodService.validateSchedulingPeriodAsync(orgId, spId)
            .ThrowsAsync(new Exception("Not found"));

        Assert.ThrowsAsync<Exception>(() =>
            _sut.validateSchedulingPeriodAsync(orgId, spId));
    }

    [Test]
    public void GivenValidPeriod_WhenValidate_ThenDelegatesWithoutError()
    {
        var orgId = Guid.NewGuid();
        var spId = Guid.NewGuid();

        Assert.DoesNotThrowAsync(() =>
            _sut.validateSchedulingPeriodAsync(orgId, spId));
    }
}
