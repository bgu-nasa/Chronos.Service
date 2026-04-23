using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Resources;
using Chronos.Domain.Schedule;
using Chronos.MainApi.Schedule.Services;
using Chronos.MainApi.Shared.ExternalMangement;
using Chronos.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Chronos.Tests.MainApi.Services.Schedule;

[TestFixture]
[Category("Unit")]
public class AssignmentServiceTests
{
    private IAssignmentRepository _assignmentRepository = null!;
    private IManagementExternalService _validationService = null!;
    private IExternalActivityService _activityService = null!;
    private IExternalSubjectService _subjectService = null!;
    private ISlotService _slotService = null!;
    private IExternalResourceService _resourceService = null!;
    private ISchedulingPeriodService _schedulingPeriodService = null!;
    private ILogger<AssignmentService> _logger = null!;
    private AssignmentService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _assignmentRepository = Substitute.For<IAssignmentRepository>();
        _validationService = Substitute.For<IManagementExternalService>();
        _activityService = Substitute.For<IExternalActivityService>();
        _subjectService = Substitute.For<IExternalSubjectService>();
        _slotService = Substitute.For<ISlotService>();
        _resourceService = Substitute.For<IExternalResourceService>();
        _schedulingPeriodService = Substitute.For<ISchedulingPeriodService>();
        _logger = Substitute.For<ILogger<AssignmentService>>();

        _service = new AssignmentService(
            _assignmentRepository,
            _validationService,
            _activityService,
            _subjectService,
            _slotService,
            _resourceService,
            _schedulingPeriodService,
            _logger);
    }

    private void SetupValidAssignment(Guid organizationId, Guid slotId, Guid resourceId, Guid activityId,
        Guid subjectId, Guid schedulingPeriodId, int? resourceCapacity = 50, int? expectedStudents = 30)
    {
        var resource = new Resource
        {
            Id = resourceId,
            OrganizationId = organizationId,
            ResourceTypeId = Guid.NewGuid(),
            Location = "Building A",
            Identifier = "Room 101",
            Capacity = resourceCapacity
        };

        var activity = new Activity
        {
            Id = activityId,
            OrganizationId = organizationId,
            SubjectId = subjectId,
            AssignedUserId = Guid.NewGuid(),
            ActivityType = "Lecture",
            ExpectedStudents = expectedStudents
        };

        var subject = new Subject
        {
            Id = subjectId,
            OrganizationId = organizationId,
            DepartmentId = Guid.NewGuid(),
            SchedulingPeriodId = schedulingPeriodId,
            Code = "CS101",
            Name = "Intro to CS"
        };

        var schedulingPeriod = new SchedulingPeriod
        {
            Id = schedulingPeriodId,
            OrganizationId = organizationId,
            Name = "Fall 2024",
            FromDate = DateTime.Today,
            ToDate = DateTime.Today.AddDays(90)
        };

        var slot = new Slot
        {
            Id = slotId,
            OrganizationId = organizationId,
            SchedulingPeriodId = schedulingPeriodId,
            Weekday = "Monday",
            FromTime = TimeSpan.FromHours(9),
            ToTime = TimeSpan.FromHours(10)
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _resourceService.GetResourceAsync(organizationId, resourceId).Returns(resource);
        _activityService.GetActivityAsync(organizationId, activityId).Returns(activity);
        _subjectService.GetSubjectAsync(organizationId, subjectId).Returns(subject);
        _schedulingPeriodService.GetSchedulingPeriodAsync(organizationId, schedulingPeriodId).Returns(schedulingPeriod);
        _slotService.GetSlotAsync(organizationId, slotId).Returns(slot);
        _assignmentRepository.GetByResourceIdAsync(resourceId).Returns(new List<Assignment>());
    }

    #region CreateAssignmentAsync Tests

    [Test]
    public async Task CreateAssignmentAsync_WithValidData_ReturnsNewId()
    {
        var organizationId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();

        SetupValidAssignment(organizationId, slotId, resourceId, activityId, subjectId, schedulingPeriodId);

        var result = await _service.CreateAssignmentAsync(organizationId, slotId, resourceId, activityId);

        Assert.That(result, Is.Not.EqualTo(Guid.Empty));
        await _assignmentRepository.Received(1).AddAsync(Arg.Is<Assignment>(a =>
            a.OrganizationId == organizationId &&
            a.SlotId == slotId &&
            a.ResourceId == resourceId &&
            a.ActivityId == activityId));
    }

    [Test]
    public void CreateAssignmentAsync_WithNonExistentResource_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _resourceService.GetResourceAsync(organizationId, resourceId).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.CreateAssignmentAsync(organizationId, slotId, resourceId, activityId));

        Assert.That(ex!.Message, Does.Contain("Resource"));
    }

    [Test]
    public void CreateAssignmentAsync_WithNonExistentActivity_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        var resource = new Resource
        {
            Id = resourceId,
            OrganizationId = organizationId,
            ResourceTypeId = Guid.NewGuid(),
            Location = "Building A",
            Identifier = "Room 101",
            Capacity = 50
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _resourceService.GetResourceAsync(organizationId, resourceId).Returns(resource);
        _activityService.GetActivityAsync(organizationId, activityId).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.CreateAssignmentAsync(organizationId, slotId, resourceId, activityId));

        Assert.That(ex!.Message, Does.Contain("Activity"));
    }

    [Test]
    public void CreateAssignmentAsync_WithInsufficientResourceCapacity_ThrowsBadRequestException()
    {
        var organizationId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();

        SetupValidAssignment(organizationId, slotId, resourceId, activityId, subjectId, schedulingPeriodId,
            resourceCapacity: 20, expectedStudents: 50);

        var ex = Assert.ThrowsAsync<BadRequestException>(async () =>
            await _service.CreateAssignmentAsync(organizationId, slotId, resourceId, activityId));

        Assert.That(ex!.Message, Does.Contain("capacity"));
    }

    [Test]
    public void CreateAssignmentAsync_WithSlotInDifferentSchedulingPeriod_ThrowsBadRequestException()
    {
        var organizationId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();
        var differentSchedulingPeriodId = Guid.NewGuid();

        var resource = new Resource
        {
            Id = resourceId,
            OrganizationId = organizationId,
            ResourceTypeId = Guid.NewGuid(),
            Location = "Building A",
            Identifier = "Room 101",
            Capacity = 50
        };

        var activity = new Activity
        {
            Id = activityId,
            OrganizationId = organizationId,
            SubjectId = subjectId,
            AssignedUserId = Guid.NewGuid(),
            ActivityType = "Lecture",
            ExpectedStudents = 30
        };

        var subject = new Subject
        {
            Id = subjectId,
            OrganizationId = organizationId,
            DepartmentId = Guid.NewGuid(),
            SchedulingPeriodId = schedulingPeriodId,
            Code = "CS101",
            Name = "Intro to CS"
        };

        var schedulingPeriod = new SchedulingPeriod
        {
            Id = schedulingPeriodId,
            OrganizationId = organizationId,
            Name = "Fall 2024",
            FromDate = DateTime.Today,
            ToDate = DateTime.Today.AddDays(90)
        };

        var slot = new Slot
        {
            Id = slotId,
            OrganizationId = organizationId,
            SchedulingPeriodId = differentSchedulingPeriodId,
            Weekday = "Monday",
            FromTime = TimeSpan.FromHours(9),
            ToTime = TimeSpan.FromHours(10)
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _resourceService.GetResourceAsync(organizationId, resourceId).Returns(resource);
        _activityService.GetActivityAsync(organizationId, activityId).Returns(activity);
        _subjectService.GetSubjectAsync(organizationId, subjectId).Returns(subject);
        _schedulingPeriodService.GetSchedulingPeriodAsync(organizationId, schedulingPeriodId).Returns(schedulingPeriod);
        _slotService.GetSlotAsync(organizationId, slotId).Returns(slot);

        var ex = Assert.ThrowsAsync<BadRequestException>(async () =>
            await _service.CreateAssignmentAsync(organizationId, slotId, resourceId, activityId));

        Assert.That(ex!.Message, Does.Contain("scheduling period"));
    }

    [Test]
    public void CreateAssignmentAsync_WithResourceAlreadyAssignedInOverlappingSlot_ThrowsBadRequestException()
    {
        var organizationId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var existingSlotId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();

        SetupValidAssignment(organizationId, slotId, resourceId, activityId, subjectId, schedulingPeriodId);

        var existingAssignment = new Assignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            SlotId = existingSlotId,
            ResourceId = resourceId,
            ActivityId = Guid.NewGuid()
        };

        var existingSlot = new Slot
        {
            Id = existingSlotId,
            OrganizationId = organizationId,
            SchedulingPeriodId = schedulingPeriodId,
            Weekday = "Monday",
            FromTime = TimeSpan.FromHours(9),
            ToTime = TimeSpan.FromHours(11)
        };

        _assignmentRepository.GetByResourceIdAsync(resourceId).Returns(new List<Assignment> { existingAssignment });
        _slotService.GetSlotAsync(organizationId, existingSlotId).Returns(existingSlot);

        var ex = Assert.ThrowsAsync<BadRequestException>(async () =>
            await _service.CreateAssignmentAsync(organizationId, slotId, resourceId, activityId));

        Assert.That(ex!.Message, Does.Contain("overlaps"));
    }

    [Test]
    public async Task CreateAssignmentAsync_WithResourceAssignedOnDifferentDay_Succeeds()
    {
        var organizationId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var existingSlotId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();

        SetupValidAssignment(organizationId, slotId, resourceId, activityId, subjectId, schedulingPeriodId);

        var existingAssignment = new Assignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            SlotId = existingSlotId,
            ResourceId = resourceId,
            ActivityId = Guid.NewGuid()
        };

        var existingSlot = new Slot
        {
            Id = existingSlotId,
            OrganizationId = organizationId,
            SchedulingPeriodId = schedulingPeriodId,
            Weekday = "Tuesday",
            FromTime = TimeSpan.FromHours(9),
            ToTime = TimeSpan.FromHours(10)
        };

        _assignmentRepository.GetByResourceIdAsync(resourceId).Returns(new List<Assignment> { existingAssignment });
        _slotService.GetSlotAsync(organizationId, existingSlotId).Returns(existingSlot);

        var result = await _service.CreateAssignmentAsync(organizationId, slotId, resourceId, activityId);

        Assert.That(result, Is.Not.EqualTo(Guid.Empty));
    }

    [Test]
    public async Task CreateAssignmentAsync_WithNullExpectedStudents_Succeeds()
    {
        var organizationId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();

        SetupValidAssignment(organizationId, slotId, resourceId, activityId, subjectId, schedulingPeriodId,
            resourceCapacity: 50, expectedStudents: null);

        var result = await _service.CreateAssignmentAsync(organizationId, slotId, resourceId, activityId);

        Assert.That(result, Is.Not.EqualTo(Guid.Empty));
    }

    #endregion

    #region GetAssignmentAsync Tests

    [Test]
    public async Task GetAssignmentAsync_WithExistingAssignment_ReturnsAssignment()
    {
        var organizationId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var assignment = new Assignment
        {
            Id = assignmentId,
            OrganizationId = organizationId,
            SlotId = Guid.NewGuid(),
            ResourceId = Guid.NewGuid(),
            ActivityId = Guid.NewGuid()
        };

        _assignmentRepository.GetByIdAsync(assignmentId).Returns(assignment);

        var result = await _service.GetAssignmentAsync(organizationId, assignmentId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(assignmentId));
    }

    [Test]
    public void GetAssignmentAsync_WithNonExistentAssignment_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        _assignmentRepository.GetByIdAsync(assignmentId).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.GetAssignmentAsync(organizationId, assignmentId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public void GetAssignmentAsync_WithWrongOrganization_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var wrongOrgId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var assignment = new Assignment
        {
            Id = assignmentId,
            OrganizationId = wrongOrgId,
            SlotId = Guid.NewGuid(),
            ResourceId = Guid.NewGuid(),
            ActivityId = Guid.NewGuid()
        };

        _assignmentRepository.GetByIdAsync(assignmentId).Returns(assignment);

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.GetAssignmentAsync(organizationId, assignmentId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion

    #region GetAllAssignmentsAsync Tests

    [Test]
    public async Task GetAllAssignmentsAsync_ReturnsOnlyOrganizationAssignments()
    {
        var organizationId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var assignments = new List<Assignment>
        {
            new Assignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SlotId = Guid.NewGuid(),
                ResourceId = Guid.NewGuid(),
                ActivityId = Guid.NewGuid()
            },
            new Assignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrgId,
                SlotId = Guid.NewGuid(),
                ResourceId = Guid.NewGuid(),
                ActivityId = Guid.NewGuid()
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _assignmentRepository.GetAllAsync(Arg.Any<AssignmentQuery>())
            .Returns(assignments.Where(a => a.OrganizationId == organizationId).ToList());

        var result = await _service.GetAllAssignmentsAsync(organizationId);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result.All(a => a.OrganizationId == organizationId), Is.True);
    }

    #endregion

    #region GetAssignmentsBySlotAsync Tests

    [Test]
    public async Task GetAssignmentsBySlotAsync_ReturnsFilteredAssignments()
    {
        var organizationId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
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

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _assignmentRepository.GetBySlotIdAsync(slotId).Returns(assignments);

        var result = await _service.GetAssignmentsBySlotAsync(organizationId, slotId);

        Assert.That(result, Has.Count.EqualTo(1));
    }

    #endregion

    #region GetAssignmentsByActivityIdAsync Tests

    [Test]
    public async Task GetAssignmentsByActivityIdAsync_ReturnsFilteredAssignments()
    {
        var organizationId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var assignments = new List<Assignment>
        {
            new Assignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SlotId = Guid.NewGuid(),
                ResourceId = Guid.NewGuid(),
                ActivityId = activityId
            }
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _assignmentRepository.GetByActivityIdAsync(activityId).Returns(assignments);

        var result = await _service.GetAssignmentsByActivityIdAsync(organizationId, activityId);

        Assert.That(result, Has.Count.EqualTo(1));
    }

    #endregion

    #region GetAssignmentBySlotAndResourceItemAsync Tests

    [Test]
    public async Task GetAssignmentBySlotAndResourceItemAsync_WithExistingAssignment_ReturnsAssignment()
    {
        var organizationId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var assignment = new Assignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            SlotId = slotId,
            ResourceId = resourceId,
            ActivityId = Guid.NewGuid()
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _assignmentRepository.GetBySlotIdAndResourceIdAsync(slotId, resourceId).Returns(assignment);

        var result = await _service.GetAssignmentBySlotAndResourceItemAsync(organizationId, slotId, resourceId);

        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task GetAssignmentBySlotAndResourceItemAsync_WithNonExistentAssignment_ReturnsNull()
    {
        var organizationId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _assignmentRepository.GetBySlotIdAndResourceIdAsync(slotId, resourceId).ReturnsNull();

        var result = await _service.GetAssignmentBySlotAndResourceItemAsync(organizationId, slotId, resourceId);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetAssignmentBySlotAndResourceItemAsync_WithWrongOrganization_ReturnsNull()
    {
        var organizationId = Guid.NewGuid();
        var wrongOrgId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var assignment = new Assignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = wrongOrgId,
            SlotId = slotId,
            ResourceId = resourceId,
            ActivityId = Guid.NewGuid()
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _assignmentRepository.GetBySlotIdAndResourceIdAsync(slotId, resourceId).Returns(assignment);

        var result = await _service.GetAssignmentBySlotAndResourceItemAsync(organizationId, slotId, resourceId);

        Assert.That(result, Is.Null);
    }

    #endregion

    #region UpdateAssignmentAsync Tests

    [Test]
    public async Task UpdateAssignmentAsync_WithValidData_UpdatesAssignment()
    {
        var organizationId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();

        var assignment = new Assignment
        {
            Id = assignmentId,
            OrganizationId = organizationId,
            SlotId = Guid.NewGuid(),
            ResourceId = Guid.NewGuid(),
            ActivityId = Guid.NewGuid()
        };

        _assignmentRepository.GetByIdAsync(assignmentId).Returns(assignment);
        SetupValidAssignment(organizationId, slotId, resourceId, activityId, subjectId, schedulingPeriodId);

        await _service.UpdateAssignmentAsync(organizationId, assignmentId, slotId, resourceId, activityId);

        await _assignmentRepository.Received(1).UpdateAsync(Arg.Is<Assignment>(a =>
            a.SlotId == slotId &&
            a.ResourceId == resourceId &&
            a.ActivityId == activityId));
    }

    [Test]
    public void UpdateAssignmentAsync_WithNonExistentAssignment_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        _assignmentRepository.GetByIdAsync(assignmentId).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.UpdateAssignmentAsync(organizationId, assignmentId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public async Task UpdateAssignmentAsync_ExcludesSelfFromOverlapCheck_Succeeds()
    {
        var organizationId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var schedulingPeriodId = Guid.NewGuid();

        var assignment = new Assignment
        {
            Id = assignmentId,
            OrganizationId = organizationId,
            SlotId = slotId,
            ResourceId = resourceId,
            ActivityId = activityId
        };

        _assignmentRepository.GetByIdAsync(assignmentId).Returns(assignment);
        SetupValidAssignment(organizationId, slotId, resourceId, activityId, subjectId, schedulingPeriodId);
        _assignmentRepository.GetByResourceIdAsync(resourceId).Returns(new List<Assignment> { assignment });

        await _service.UpdateAssignmentAsync(organizationId, assignmentId, slotId, resourceId, activityId);

        await _assignmentRepository.Received(1).UpdateAsync(Arg.Any<Assignment>());
    }

    #endregion

    #region DeleteAssignmentAsync Tests

    [Test]
    public async Task DeleteAssignmentAsync_WithExistingAssignment_DeletesAssignment()
    {
        var organizationId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var assignment = new Assignment
        {
            Id = assignmentId,
            OrganizationId = organizationId,
            SlotId = Guid.NewGuid(),
            ResourceId = Guid.NewGuid(),
            ActivityId = Guid.NewGuid()
        };

        _assignmentRepository.GetByIdAsync(assignmentId).Returns(assignment);

        await _service.DeleteAssignmentAsync(organizationId, assignmentId);

        await _assignmentRepository.Received(1).DeleteAsync(assignment);
    }

    [Test]
    public void DeleteAssignmentAsync_WithNonExistentAssignment_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        _assignmentRepository.GetByIdAsync(assignmentId).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.DeleteAssignmentAsync(organizationId, assignmentId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public void DeleteAssignmentAsync_WithWrongOrganization_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var wrongOrgId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var assignment = new Assignment
        {
            Id = assignmentId,
            OrganizationId = wrongOrgId,
            SlotId = Guid.NewGuid(),
            ResourceId = Guid.NewGuid(),
            ActivityId = Guid.NewGuid()
        };

        _assignmentRepository.GetByIdAsync(assignmentId).Returns(assignment);

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.DeleteAssignmentAsync(organizationId, assignmentId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion
}
