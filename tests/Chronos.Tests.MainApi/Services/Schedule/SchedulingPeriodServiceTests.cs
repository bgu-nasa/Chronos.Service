using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Schedule;
using Chronos.MainApi.Schedule.Services;
using Chronos.MainApi.Shared.ExternalMangement;
using Chronos.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Chronos.Tests.MainApi.Services.Schedule;

[TestFixture]
public class SchedulingPeriodServiceTests
{
    private ISchedulingPeriodRepository _schedulingPeriodRepository = null!;
    private IManagementExternalService _validationService = null!;
    private ILogger<SchedulingPeriodService> _logger = null!;
    private SchedulingPeriodService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _schedulingPeriodRepository = Substitute.For<ISchedulingPeriodRepository>();
        _validationService = Substitute.For<IManagementExternalService>();
        _logger = Substitute.For<ILogger<SchedulingPeriodService>>();

        _service = new SchedulingPeriodService(
            _schedulingPeriodRepository,
            _validationService,
            _logger);
    }

    #region CreateSchedulingPeriodAsync Tests

    [Test]
    public async Task CreateSchedulingPeriodAsync_WithValidData_ReturnsNewId()
    {
        var organizationId = Guid.NewGuid();
        var name = "Fall 2024";
        var fromDate = DateTime.Today.AddDays(1);
        var toDate = DateTime.Today.AddDays(30);

        // Convert to UTC to match what the service does
        var fromDateUtc = fromDate.Kind == DateTimeKind.Utc 
            ? fromDate 
            : (fromDate.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(fromDate, DateTimeKind.Utc)
                : fromDate.ToUniversalTime());
        var toDateUtc = toDate.Kind == DateTimeKind.Utc 
            ? toDate 
            : (toDate.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(toDate, DateTimeKind.Utc)
                : toDate.ToUniversalTime());

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _schedulingPeriodRepository.GetAllAsync().Returns(new List<SchedulingPeriod>());

        var result = await _service.CreateSchedulingPeriodAsync(organizationId, name, fromDate, toDate);

        Assert.That(result, Is.Not.EqualTo(Guid.Empty));
        await _schedulingPeriodRepository.Received(1).AddAsync(Arg.Is<SchedulingPeriod>(p =>
            p.OrganizationId == organizationId &&
            p.Name == name &&
            p.FromDate == fromDateUtc &&
            p.ToDate == toDateUtc));
    }

    [Test]
    public void CreateSchedulingPeriodAsync_WithFromDateInPast_ThrowsBadRequestException()
    {
        var organizationId = Guid.NewGuid();
        var name = "Past Period";
        var fromDate = DateTime.Today.AddDays(-1);
        var toDate = DateTime.Today.AddDays(30);

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);

        var ex = Assert.ThrowsAsync<BadRequestException>(async () =>
            await _service.CreateSchedulingPeriodAsync(organizationId, name, fromDate, toDate));

        Assert.That(ex!.Message, Does.Contain("cannot be in the past"));
    }

    [Test]
    public void CreateSchedulingPeriodAsync_WithToDateInPast_ThrowsBadRequestException()
    {
        var organizationId = Guid.NewGuid();
        var name = "Past Period";
        var fromDate = DateTime.Today.AddDays(1);
        var toDate = DateTime.Today.AddDays(-1);

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);

        var ex = Assert.ThrowsAsync<BadRequestException>(async () =>
            await _service.CreateSchedulingPeriodAsync(organizationId, name, fromDate, toDate));

        Assert.That(ex!.Message, Does.Contain("cannot be in the past"));
    }

    [Test]
    public void CreateSchedulingPeriodAsync_WithFromDateAfterToDate_ThrowsBadRequestException()
    {
        var organizationId = Guid.NewGuid();
        var name = "Invalid Period";
        var fromDate = DateTime.Today.AddDays(30);
        var toDate = DateTime.Today.AddDays(10);

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);

        var ex = Assert.ThrowsAsync<BadRequestException>(async () =>
            await _service.CreateSchedulingPeriodAsync(organizationId, name, fromDate, toDate));

        Assert.That(ex!.Message, Does.Contain("FromDate must be before"));
    }

    [Test]
    public void CreateSchedulingPeriodAsync_WithOverlappingPeriod_ThrowsBadRequestException()
    {
        var organizationId = Guid.NewGuid();
        var existingPeriod = new SchedulingPeriod
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = "Existing Period",
            FromDate = DateTime.Today.AddDays(5),
            ToDate = DateTime.Today.AddDays(20)
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _schedulingPeriodRepository.GetAllAsync().Returns(new List<SchedulingPeriod> { existingPeriod });

        var ex = Assert.ThrowsAsync<BadRequestException>(async () =>
            await _service.CreateSchedulingPeriodAsync(organizationId, "New Period",
                DateTime.Today.AddDays(10), DateTime.Today.AddDays(25)));

        Assert.That(ex!.Message, Does.Contain("overlaps"));
    }

    [Test]
    public async Task CreateSchedulingPeriodAsync_WithNonOverlappingPeriod_Succeeds()
    {
        var organizationId = Guid.NewGuid();
        var existingPeriod = new SchedulingPeriod
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = "Existing Period",
            FromDate = DateTime.Today.AddDays(5),
            ToDate = DateTime.Today.AddDays(20)
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _schedulingPeriodRepository.GetAllAsync().Returns(new List<SchedulingPeriod> { existingPeriod });

        var result = await _service.CreateSchedulingPeriodAsync(organizationId, "New Period",
            DateTime.Today.AddDays(25), DateTime.Today.AddDays(40));

        Assert.That(result, Is.Not.EqualTo(Guid.Empty));
    }

    #endregion

    #region GetSchedulingPeriodAsync Tests

    [Test]
    public async Task GetSchedulingPeriodAsync_WithExistingPeriod_ReturnsPeriod()
    {
        var organizationId = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        var period = new SchedulingPeriod
        {
            Id = periodId,
            OrganizationId = organizationId,
            Name = "Test Period",
            FromDate = DateTime.Today,
            ToDate = DateTime.Today.AddDays(30)
        };

        _schedulingPeriodRepository.GetByIdAsync(periodId).Returns(period);

        var result = await _service.GetSchedulingPeriodAsync(organizationId, periodId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(periodId));
        Assert.That(result.Name, Is.EqualTo("Test Period"));
    }

    [Test]
    public void GetSchedulingPeriodAsync_WithNonExistentPeriod_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var periodId = Guid.NewGuid();

        _schedulingPeriodRepository.GetByIdAsync(periodId).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.GetSchedulingPeriodAsync(organizationId, periodId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public void GetSchedulingPeriodAsync_WithWrongOrganization_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var wrongOrganizationId = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        var period = new SchedulingPeriod
        {
            Id = periodId,
            OrganizationId = wrongOrganizationId,
            Name = "Test Period",
            FromDate = DateTime.Today,
            ToDate = DateTime.Today.AddDays(30)
        };

        _schedulingPeriodRepository.GetByIdAsync(periodId).Returns(period);

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.GetSchedulingPeriodAsync(organizationId, periodId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion

    #region GetSchedulingPeriodByNameAsync Tests

    [Test]
    public async Task GetSchedulingPeriodByNameAsync_WithExistingName_ReturnsPeriod()
    {
        var organizationId = Guid.NewGuid();
        var name = "Fall 2024";
        var period = new SchedulingPeriod
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            FromDate = DateTime.Today,
            ToDate = DateTime.Today.AddDays(30)
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _schedulingPeriodRepository.GetByNameAsync(name).Returns(period);

        var result = await _service.GetSchedulingPeriodByNameAsync(organizationId, name);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo(name));
    }

    [Test]
    public void GetSchedulingPeriodByNameAsync_WithNonExistentName_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var name = "NonExistent";

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _schedulingPeriodRepository.GetByNameAsync(name).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.GetSchedulingPeriodByNameAsync(organizationId, name));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion

    #region GetAllSchedulingPeriodsAsync Tests

    [Test]
    public async Task GetAllSchedulingPeriodsAsync_ReturnsOnlyOrganizationPeriods()
    {
        var organizationId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var periods = new List<SchedulingPeriod>
        {
            new SchedulingPeriod
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "Period 1",
                FromDate = DateTime.Today,
                ToDate = DateTime.Today.AddDays(30)
            },
            new SchedulingPeriod
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "Period 2",
                FromDate = DateTime.Today.AddDays(40),
                ToDate = DateTime.Today.AddDays(70)
            },
            new SchedulingPeriod
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrgId,
                Name = "Other Org Period",
                FromDate = DateTime.Today,
                ToDate = DateTime.Today.AddDays(30)
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _schedulingPeriodRepository.GetAllAsync().Returns(periods);

        var result = await _service.GetAllSchedulingPeriodsAsync(organizationId);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.All(p => p.OrganizationId == organizationId), Is.True);
    }

    [Test]
    public async Task GetAllSchedulingPeriodsAsync_ReturnsSortedByFromDate()
    {
        var organizationId = Guid.NewGuid();
        var periods = new List<SchedulingPeriod>
        {
            new SchedulingPeriod
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "Period 2",
                FromDate = DateTime.Today.AddDays(40),
                ToDate = DateTime.Today.AddDays(70)
            },
            new SchedulingPeriod
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "Period 1",
                FromDate = DateTime.Today,
                ToDate = DateTime.Today.AddDays(30)
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _schedulingPeriodRepository.GetAllAsync().Returns(periods);

        var result = await _service.GetAllSchedulingPeriodsAsync(organizationId);

        Assert.That(result[0].Name, Is.EqualTo("Period 1"));
        Assert.That(result[1].Name, Is.EqualTo("Period 2"));
    }

    #endregion

    #region UpdateSchedulingPeriodAsync Tests

    [Test]
    public async Task UpdateSchedulingPeriodAsync_WithValidData_UpdatesPeriod()
    {
        var organizationId = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        var period = new SchedulingPeriod
        {
            Id = periodId,
            OrganizationId = organizationId,
            Name = "Old Name",
            FromDate = DateTime.Today.AddDays(1),
            ToDate = DateTime.Today.AddDays(30)
        };

        _schedulingPeriodRepository.GetByIdAsync(periodId).Returns(period);
        _schedulingPeriodRepository.GetAllAsync().Returns(new List<SchedulingPeriod> { period });

        var newFromDate = DateTime.Today.AddDays(5);
        var newToDate = DateTime.Today.AddDays(35);

        // Convert to UTC to match what the service does
        var newFromDateUtc = newFromDate.Kind == DateTimeKind.Utc 
            ? newFromDate 
            : (newFromDate.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(newFromDate, DateTimeKind.Utc)
                : newFromDate.ToUniversalTime());
        var newToDateUtc = newToDate.Kind == DateTimeKind.Utc 
            ? newToDate 
            : (newToDate.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(newToDate, DateTimeKind.Utc)
                : newToDate.ToUniversalTime());

        await _service.UpdateSchedulingPeriodAsync(organizationId, periodId, "New Name", newFromDate, newToDate);

        await _schedulingPeriodRepository.Received(1).UpdateAsync(Arg.Is<SchedulingPeriod>(p =>
            p.Name == "New Name" &&
            p.FromDate == newFromDateUtc &&
            p.ToDate == newToDateUtc));
    }

    [Test]
    public void UpdateSchedulingPeriodAsync_WithNonExistentPeriod_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var periodId = Guid.NewGuid();

        _schedulingPeriodRepository.GetByIdAsync(periodId).ReturnsNull();
        _schedulingPeriodRepository.GetAllAsync().Returns(new List<SchedulingPeriod>());

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.UpdateSchedulingPeriodAsync(organizationId, periodId, "Name",
                DateTime.Today.AddDays(1), DateTime.Today.AddDays(30)));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public void UpdateSchedulingPeriodAsync_WithOverlappingPeriod_ThrowsBadRequestException()
    {
        var organizationId = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        var existingPeriodId = Guid.NewGuid();

        var period = new SchedulingPeriod
        {
            Id = periodId,
            OrganizationId = organizationId,
            Name = "Current Period",
            FromDate = DateTime.Today.AddDays(1),
            ToDate = DateTime.Today.AddDays(10)
        };

        var existingPeriod = new SchedulingPeriod
        {
            Id = existingPeriodId,
            OrganizationId = organizationId,
            Name = "Existing Period",
            FromDate = DateTime.Today.AddDays(20),
            ToDate = DateTime.Today.AddDays(40)
        };

        _schedulingPeriodRepository.GetByIdAsync(periodId).Returns(period);
        _schedulingPeriodRepository.GetAllAsync().Returns(new List<SchedulingPeriod> { period, existingPeriod });

        var ex = Assert.ThrowsAsync<BadRequestException>(async () =>
            await _service.UpdateSchedulingPeriodAsync(organizationId, periodId, "Updated",
                DateTime.Today.AddDays(15), DateTime.Today.AddDays(35)));

        Assert.That(ex!.Message, Does.Contain("overlaps"));
    }

    [Test]
    public async Task UpdateSchedulingPeriodAsync_CanUpdateToSameDateRange_Succeeds()
    {
        var organizationId = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        var period = new SchedulingPeriod
        {
            Id = periodId,
            OrganizationId = organizationId,
            Name = "Period",
            FromDate = DateTime.Today.AddDays(1),
            ToDate = DateTime.Today.AddDays(30)
        };

        _schedulingPeriodRepository.GetByIdAsync(periodId).Returns(period);
        _schedulingPeriodRepository.GetAllAsync().Returns(new List<SchedulingPeriod> { period });

        await _service.UpdateSchedulingPeriodAsync(organizationId, periodId, "Updated Name",
            DateTime.Today.AddDays(1), DateTime.Today.AddDays(30));

        await _schedulingPeriodRepository.Received(1).UpdateAsync(Arg.Any<SchedulingPeriod>());
    }

    #endregion

    #region DeleteSchedulingPeriodAsync Tests

    [Test]
    public async Task DeleteSchedulingPeriodAsync_WithExistingPeriod_DeletesPeriod()
    {
        var organizationId = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        var period = new SchedulingPeriod
        {
            Id = periodId,
            OrganizationId = organizationId,
            Name = "Test Period",
            FromDate = DateTime.Today,
            ToDate = DateTime.Today.AddDays(30)
        };

        _schedulingPeriodRepository.GetByIdAsync(periodId).Returns(period);

        await _service.DeleteSchedulingPeriodAsync(organizationId, periodId);

        await _schedulingPeriodRepository.Received(1).DeleteAsync(period);
    }

    [Test]
    public void DeleteSchedulingPeriodAsync_WithNonExistentPeriod_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var periodId = Guid.NewGuid();

        _schedulingPeriodRepository.GetByIdAsync(periodId).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.DeleteSchedulingPeriodAsync(organizationId, periodId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public void DeleteSchedulingPeriodAsync_WithWrongOrganization_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var wrongOrganizationId = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        var period = new SchedulingPeriod
        {
            Id = periodId,
            OrganizationId = wrongOrganizationId,
            Name = "Test Period",
            FromDate = DateTime.Today,
            ToDate = DateTime.Today.AddDays(30)
        };

        _schedulingPeriodRepository.GetByIdAsync(periodId).Returns(period);

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.DeleteSchedulingPeriodAsync(organizationId, periodId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion
}
