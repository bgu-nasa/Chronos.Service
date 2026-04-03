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
public class SlotServiceTests
{
    private ISlotRepository _slotRepository = null!;
    private IAssignmentRepository _assignmentRepository = null!;
    private IManagementExternalService _validationService = null!;
    private ISchedulingPeriodService _schedulingPeriodService = null!;
    private ILogger<SlotService> _logger = null!;
    private SlotService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _slotRepository = Substitute.For<ISlotRepository>();
        _assignmentRepository = Substitute.For<IAssignmentRepository>();
        _validationService = Substitute.For<IManagementExternalService>();
        _schedulingPeriodService = Substitute.For<ISchedulingPeriodService>();
        _logger = Substitute.For<ILogger<SlotService>>();

        _service = new SlotService(
            _slotRepository,
            _assignmentRepository,
            _validationService,
            _schedulingPeriodService,
            _logger);
    }

    #region CreateSlotAsync Tests

    [Test]
    public async Task CreateSlotAsync_WithValidData_ReturnsNewId()
    {
        var organizationId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var weekday = WeekDays.Monday;
        var fromTime = TimeSpan.FromHours(9);
        var toTime = TimeSpan.FromHours(10);

        var period = new SchedulingPeriod
        {
            Id = schedulingPeriodId,
            OrganizationId = organizationId,
            Name = "Test Period",
            FromDate = DateTime.Today,
            ToDate = DateTime.Today.AddDays(30)
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _schedulingPeriodService.GetSchedulingPeriodAsync(organizationId, schedulingPeriodId).Returns(period);
        _slotRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId, 1, int.MaxValue).Returns((new List<Slot>(), 0));

        var result = await _service.CreateSlotAsync(organizationId, schedulingPeriodId, weekday, fromTime, toTime);

        Assert.That(result, Is.Not.EqualTo(Guid.Empty));
        await _slotRepository.Received(1).AddAsync(Arg.Is<Slot>(s =>
            s.OrganizationId == organizationId &&
            s.SchedulingPeriodId == schedulingPeriodId &&
            s.Weekday == weekday.ToString() &&
            s.FromTime == fromTime &&
            s.ToTime == toTime));
    }

    [Test]
    public void CreateSlotAsync_WithFromTimeAfterToTime_ThrowsBadRequestException()
    {
        var organizationId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var fromTime = TimeSpan.FromHours(10);
        var toTime = TimeSpan.FromHours(9);

        var period = new SchedulingPeriod
        {
            Id = schedulingPeriodId,
            OrganizationId = organizationId,
            Name = "Test Period",
            FromDate = DateTime.Today,
            ToDate = DateTime.Today.AddDays(30)
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _schedulingPeriodService.GetSchedulingPeriodAsync(organizationId, schedulingPeriodId).Returns(period);

        var ex = Assert.ThrowsAsync<BadRequestException>(async () =>
            await _service.CreateSlotAsync(organizationId, schedulingPeriodId, WeekDays.Monday, fromTime, toTime));

        Assert.That(ex!.Message, Does.Contain("FromTime must be earlier than ToTime"));
    }

    [Test]
    public void CreateSlotAsync_WithNegativeFromTime_ThrowsBadRequestException()
    {
        var organizationId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var fromTime = TimeSpan.FromHours(-1);
        var toTime = TimeSpan.FromHours(10);

        var period = new SchedulingPeriod
        {
            Id = schedulingPeriodId,
            OrganizationId = organizationId,
            Name = "Test Period",
            FromDate = DateTime.Today,
            ToDate = DateTime.Today.AddDays(30)
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _schedulingPeriodService.GetSchedulingPeriodAsync(organizationId, schedulingPeriodId).Returns(period);

        var ex = Assert.ThrowsAsync<BadRequestException>(async () =>
            await _service.CreateSlotAsync(organizationId, schedulingPeriodId, WeekDays.Monday, fromTime, toTime));

        Assert.That(ex!.Message, Does.Contain("non-negative"));
    }

    [Test]
    public void CreateSlotAsync_WithOverlappingSlot_ThrowsBadRequestException()
    {
        var organizationId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var existingSlot = new Slot
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            SchedulingPeriodId = schedulingPeriodId,
            Weekday = "Monday",
            FromTime = TimeSpan.FromHours(9),
            ToTime = TimeSpan.FromHours(11)
        };

        var period = new SchedulingPeriod
        {
            Id = schedulingPeriodId,
            OrganizationId = organizationId,
            Name = "Test Period",
            FromDate = DateTime.Today,
            ToDate = DateTime.Today.AddDays(30)
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _schedulingPeriodService.GetSchedulingPeriodAsync(organizationId, schedulingPeriodId).Returns(period);
        _slotRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId, 1, int.MaxValue).Returns((new List<Slot> { existingSlot }, 1));

        var ex = Assert.ThrowsAsync<BadRequestException>(async () =>
            await _service.CreateSlotAsync(organizationId, schedulingPeriodId, WeekDays.Monday,
                TimeSpan.FromHours(10), TimeSpan.FromHours(12)));

        Assert.That(ex!.Message, Does.Contain("overlaps"));
    }

    [Test]
    public async Task CreateSlotAsync_WithNonOverlappingSlotOnSameDay_Succeeds()
    {
        var organizationId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var existingSlot = new Slot
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            SchedulingPeriodId = schedulingPeriodId,
            Weekday = "Monday",
            FromTime = TimeSpan.FromHours(9),
            ToTime = TimeSpan.FromHours(10)
        };

        var period = new SchedulingPeriod
        {
            Id = schedulingPeriodId,
            OrganizationId = organizationId,
            Name = "Test Period",
            FromDate = DateTime.Today,
            ToDate = DateTime.Today.AddDays(30)
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _schedulingPeriodService.GetSchedulingPeriodAsync(organizationId, schedulingPeriodId).Returns(period);
        _slotRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId, 1, int.MaxValue).Returns((new List<Slot> { existingSlot }, 1));

        var result = await _service.CreateSlotAsync(organizationId, schedulingPeriodId, WeekDays.Monday,
            TimeSpan.FromHours(11), TimeSpan.FromHours(12));

        Assert.That(result, Is.Not.EqualTo(Guid.Empty));
    }

    [Test]
    public async Task CreateSlotAsync_WithSlotOnDifferentDay_Succeeds()
    {
        var organizationId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var existingSlot = new Slot
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            SchedulingPeriodId = schedulingPeriodId,
            Weekday = "Monday",
            FromTime = TimeSpan.FromHours(9),
            ToTime = TimeSpan.FromHours(11)
        };

        var period = new SchedulingPeriod
        {
            Id = schedulingPeriodId,
            OrganizationId = organizationId,
            Name = "Test Period",
            FromDate = DateTime.Today,
            ToDate = DateTime.Today.AddDays(30)
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _schedulingPeriodService.GetSchedulingPeriodAsync(organizationId, schedulingPeriodId).Returns(period);
        _slotRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId, 1, int.MaxValue).Returns((new List<Slot> { existingSlot }, 1));

        var result = await _service.CreateSlotAsync(organizationId, schedulingPeriodId, WeekDays.Tuesday,
            TimeSpan.FromHours(9), TimeSpan.FromHours(11));

        Assert.That(result, Is.Not.EqualTo(Guid.Empty));
    }

    [Test]
    public void CreateSlotAsync_WithEndedSchedulingPeriod_ThrowsBadRequestException()
    {
        var organizationId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();

        var period = new SchedulingPeriod
        {
            Id = schedulingPeriodId,
            OrganizationId = organizationId,
            Name = "Ended Period",
            FromDate = DateTime.Today.AddDays(-30),
            ToDate = DateTime.Today.AddDays(-1)
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _schedulingPeriodService.GetSchedulingPeriodAsync(organizationId, schedulingPeriodId).Returns(period);

        var ex = Assert.ThrowsAsync<BadRequestException>(async () =>
            await _service.CreateSlotAsync(organizationId, schedulingPeriodId, WeekDays.Monday,
                TimeSpan.FromHours(9), TimeSpan.FromHours(10)));

        Assert.That(ex!.Message, Does.Contain("already ended"));
    }

    #endregion

    #region GetSlotAsync Tests

    [Test]
    public async Task GetSlotAsync_WithExistingSlot_ReturnsSlot()
    {
        var organizationId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var slot = new Slot
        {
            Id = slotId,
            OrganizationId = organizationId,
            SchedulingPeriodId = Guid.NewGuid(),
            Weekday = "Monday",
            FromTime = TimeSpan.FromHours(9),
            ToTime = TimeSpan.FromHours(10)
        };

        _slotRepository.GetByIdAsync(slotId).Returns(slot);

        var result = await _service.GetSlotAsync(organizationId, slotId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(slotId));
    }

    [Test]
    public void GetSlotAsync_WithNonExistentSlot_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var slotId = Guid.NewGuid();

        _slotRepository.GetByIdAsync(slotId).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.GetSlotAsync(organizationId, slotId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public void GetSlotAsync_WithWrongOrganization_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var wrongOrgId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var slot = new Slot
        {
            Id = slotId,
            OrganizationId = wrongOrgId,
            SchedulingPeriodId = Guid.NewGuid(),
            Weekday = "Monday",
            FromTime = TimeSpan.FromHours(9),
            ToTime = TimeSpan.FromHours(10)
        };

        _slotRepository.GetByIdAsync(slotId).Returns(slot);

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.GetSlotAsync(organizationId, slotId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion

    #region GetAllSlotsAsync Tests

    [Test]
    public async Task GetAllSlotsAsync_ReturnsOnlyOrganizationSlots()
    {
        var organizationId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var slots = new List<Slot>
        {
            new Slot
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SchedulingPeriodId = Guid.NewGuid(),
                Weekday = "Monday",
                FromTime = TimeSpan.FromHours(9),
                ToTime = TimeSpan.FromHours(10)
            },
            new Slot
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrgId,
                SchedulingPeriodId = Guid.NewGuid(),
                Weekday = "Monday",
                FromTime = TimeSpan.FromHours(9),
                ToTime = TimeSpan.FromHours(10)
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _slotRepository.GetAllAsync(1, 10).Returns((slots, slots.Count));

        var (result, totalCount) = await _service.GetAllSlotsAsync(organizationId, 1, 10);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result.All(s => s.OrganizationId == organizationId), Is.True);
    }

    [Test]
    public async Task GetAllSlotsAsync_ReturnsSortedByWeekdayThenFromTime()
    {
        var organizationId = Guid.NewGuid();
        var slots = new List<Slot>
        {
            new Slot
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SchedulingPeriodId = Guid.NewGuid(),
                Weekday = "Tuesday",
                FromTime = TimeSpan.FromHours(9),
                ToTime = TimeSpan.FromHours(10)
            },
            new Slot
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SchedulingPeriodId = Guid.NewGuid(),
                Weekday = "Monday",
                FromTime = TimeSpan.FromHours(11),
                ToTime = TimeSpan.FromHours(12)
            },
            new Slot
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SchedulingPeriodId = Guid.NewGuid(),
                Weekday = "Monday",
                FromTime = TimeSpan.FromHours(9),
                ToTime = TimeSpan.FromHours(10)
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _slotRepository.GetAllAsync(1, 10).Returns((slots, slots.Count));

        var (result, totalCount) = await _service.GetAllSlotsAsync(organizationId, 1, 10);

        Assert.That(result[0].Weekday, Is.EqualTo("Monday"));
        Assert.That(result[0].FromTime, Is.EqualTo(TimeSpan.FromHours(9)));
        Assert.That(result[1].Weekday, Is.EqualTo("Monday"));
        Assert.That(result[1].FromTime, Is.EqualTo(TimeSpan.FromHours(11)));
        Assert.That(result[2].Weekday, Is.EqualTo("Tuesday"));
    }

    #endregion

    #region GetSlotsBySchedulingPeriodAsync Tests

    [Test]
    public async Task GetSlotsBySchedulingPeriodAsync_ReturnsFilteredSlots()
    {
        var organizationId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var slots = new List<Slot>
        {
            new Slot
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SchedulingPeriodId = schedulingPeriodId,
                Weekday = "Monday",
                FromTime = TimeSpan.FromHours(9),
                ToTime = TimeSpan.FromHours(10)
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _slotRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId, 1, 10).Returns((slots, slots.Count));

        var (result, totalCount) = await _service.GetSlotsBySchedulingPeriodAsync(organizationId, schedulingPeriodId, 1, 10);

        Assert.That(result, Has.Count.EqualTo(1));
    }

    #endregion

    #region UpdateSlotAsync Tests

    [Test]
    public async Task UpdateSlotAsync_WithValidData_UpdatesSlot()
    {
        var organizationId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var slot = new Slot
        {
            Id = slotId,
            OrganizationId = organizationId,
            SchedulingPeriodId = schedulingPeriodId,
            Weekday = "Monday",
            FromTime = TimeSpan.FromHours(9),
            ToTime = TimeSpan.FromHours(10)
        };

        _slotRepository.GetByIdAsync(slotId).Returns(slot);
        _slotRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId, 1, int.MaxValue).Returns((new List<Slot> { slot }, 1));

        await _service.UpdateSlotAsync(organizationId, slotId, WeekDays.Tuesday,
            TimeSpan.FromHours(10), TimeSpan.FromHours(11));

        await _slotRepository.Received(1).UpdateAsync(Arg.Is<Slot>(s =>
            s.Weekday == "Tuesday" &&
            s.FromTime == TimeSpan.FromHours(10) &&
            s.ToTime == TimeSpan.FromHours(11)));
    }

    [Test]
    public void UpdateSlotAsync_WithOverlappingSlot_ThrowsBadRequestException()
    {
        var organizationId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var existingSlotId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();

        var slot = new Slot
        {
            Id = slotId,
            OrganizationId = organizationId,
            SchedulingPeriodId = schedulingPeriodId,
            Weekday = "Monday",
            FromTime = TimeSpan.FromHours(9),
            ToTime = TimeSpan.FromHours(10)
        };

        var existingSlot = new Slot
        {
            Id = existingSlotId,
            OrganizationId = organizationId,
            SchedulingPeriodId = schedulingPeriodId,
            Weekday = "Tuesday",
            FromTime = TimeSpan.FromHours(10),
            ToTime = TimeSpan.FromHours(12)
        };

        _slotRepository.GetByIdAsync(slotId).Returns(slot);
        _slotRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId, 1, int.MaxValue).Returns((new List<Slot> { slot, existingSlot }, 2));

        var ex = Assert.ThrowsAsync<BadRequestException>(async () =>
            await _service.UpdateSlotAsync(organizationId, slotId, WeekDays.Tuesday,
                TimeSpan.FromHours(11), TimeSpan.FromHours(13)));

        Assert.That(ex!.Message, Does.Contain("overlaps"));
    }

    [Test]
    public async Task UpdateSlotAsync_CanUpdateToSameTimeRange_Succeeds()
    {
        var organizationId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var slot = new Slot
        {
            Id = slotId,
            OrganizationId = organizationId,
            SchedulingPeriodId = schedulingPeriodId,
            Weekday = "Monday",
            FromTime = TimeSpan.FromHours(9),
            ToTime = TimeSpan.FromHours(10)
        };

        _slotRepository.GetByIdAsync(slotId).Returns(slot);
        _slotRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId, 1, int.MaxValue).Returns((new List<Slot> { slot }, 1));

        await _service.UpdateSlotAsync(organizationId, slotId, WeekDays.Monday,
            TimeSpan.FromHours(9), TimeSpan.FromHours(10));

        await _slotRepository.Received(1).UpdateAsync(Arg.Any<Slot>());
    }

    #endregion

    #region DeleteSlotAsync Tests

    [Test]
    public async Task DeleteSlotAsync_WithExistingSlot_DeletesSlotAndAssignments()
    {
        var organizationId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var slot = new Slot
        {
            Id = slotId,
            OrganizationId = organizationId,
            SchedulingPeriodId = Guid.NewGuid(),
            Weekday = "Monday",
            FromTime = TimeSpan.FromHours(9),
            ToTime = TimeSpan.FromHours(10)
        };

        var assignments = new List<Assignment>
        {
            new Assignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SlotId = slotId,
                ResourceId = Guid.NewGuid(),
                ActivityId = Guid.NewGuid()
            }
        };

        _slotRepository.GetByIdAsync(slotId).Returns(slot);
        _assignmentRepository.GetBySlotIdAsync(slotId, 1, int.MaxValue).Returns((assignments, assignments.Count));

        await _service.DeleteSlotAsync(organizationId, slotId);

        await _assignmentRepository.Received(1).DeleteAsync(assignments[0]);
        await _slotRepository.Received(1).DeleteAsync(slot);
    }

    [Test]
    public async Task DeleteSlotAsync_WithNoAssignments_DeletesSlotOnly()
    {
        var organizationId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var slot = new Slot
        {
            Id = slotId,
            OrganizationId = organizationId,
            SchedulingPeriodId = Guid.NewGuid(),
            Weekday = "Monday",
            FromTime = TimeSpan.FromHours(9),
            ToTime = TimeSpan.FromHours(10)
        };

        _slotRepository.GetByIdAsync(slotId).Returns(slot);
        _assignmentRepository.GetBySlotIdAsync(slotId, 1, int.MaxValue).Returns((new List<Assignment>(), 0));

        await _service.DeleteSlotAsync(organizationId, slotId);

        await _assignmentRepository.DidNotReceive().DeleteAsync(Arg.Any<Assignment>());
        await _slotRepository.Received(1).DeleteAsync(slot);
    }

    [Test]
    public void DeleteSlotAsync_WithNonExistentSlot_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var slotId = Guid.NewGuid();

        _slotRepository.GetByIdAsync(slotId).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.DeleteSlotAsync(organizationId, slotId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion
}
