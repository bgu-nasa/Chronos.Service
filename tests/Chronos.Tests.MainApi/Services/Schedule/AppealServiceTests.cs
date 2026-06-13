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
[Category("Unit")]
public class AppealServiceTests
{
    private IAppealRepository _appealRepository = null!;
    private IAssignmentService _assignmentService = null!;
    private IManagementExternalService _validationService = null!;
    private ILogger<AppealService> _logger = null!;
    private AppealService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _appealRepository = Substitute.For<IAppealRepository>();
        _assignmentService = Substitute.For<IAssignmentService>();
        _validationService = Substitute.For<IManagementExternalService>();
        _logger = Substitute.For<ILogger<AppealService>>();

        _service = new AppealService(
            _appealRepository,
            _assignmentService,
            _validationService,
            _logger);
    }

    private static Appeal CreateAppeal(Guid organizationId, Guid? assignmentId = null, Guid? appealId = null) =>
        new Appeal
        {
            Id = appealId ?? Guid.NewGuid(),
            OrganizationId = organizationId,
            AssignmentId = assignmentId ?? Guid.NewGuid(),
            Title = "Test Appeal",
            Description = "Test Description"
        };

    private static Assignment CreateAssignment(Guid organizationId, Guid? assignmentId = null) =>
        new Assignment
        {
            Id = assignmentId ?? Guid.NewGuid(),
            OrganizationId = organizationId,
            SlotId = Guid.NewGuid(),
            ResourceId = Guid.NewGuid(),
            ActivityId = Guid.NewGuid()
        };

    #region CreateAppealAsync Tests

    [Test]
    public async Task CreateAppealAsync_WithValidData_ReturnsNewId()
    {
        var organizationId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var assignment = CreateAssignment(organizationId, assignmentId);

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _assignmentService.GetAssignmentAsync(organizationId, assignmentId).Returns(assignment);
        _appealRepository.GetByAssignmentIdAsync(assignmentId).Returns(new List<Appeal>());

        var result = await _service.CreateAppealAsync(organizationId, assignmentId, "Title", "Description");

        Assert.That(result, Is.Not.EqualTo(Guid.Empty));
        await _appealRepository.Received(1).AddAsync(Arg.Is<Appeal>(a =>
            a.OrganizationId == organizationId &&
            a.AssignmentId == assignmentId &&
            a.Title == "Title" &&
            a.Description == "Description"));
    }

    [Test]
    public void CreateAppealAsync_WithNonExistentAssignment_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _assignmentService.GetAssignmentAsync(organizationId, assignmentId)
            .Returns(Task.FromException<Assignment>(new NotFoundException("Assignment not found.")));

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.CreateAppealAsync(organizationId, assignmentId, "Title", "Description"));
    }

    [Test]
    public void CreateAppealAsync_WhenAppealAlreadyExistsForAssignment_ThrowsBadRequestException()
    {
        var organizationId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var assignment = CreateAssignment(organizationId, assignmentId);
        var existing = CreateAppeal(organizationId, assignmentId);

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _assignmentService.GetAssignmentAsync(organizationId, assignmentId).Returns(assignment);
        _appealRepository.GetByAssignmentIdAsync(assignmentId).Returns(new List<Appeal> { existing });

        var ex = Assert.ThrowsAsync<BadRequestException>(async () =>
            await _service.CreateAppealAsync(organizationId, assignmentId, "Title", "Description"));

        Assert.That(ex!.Message, Does.Contain("already exists"));
    }

    #endregion

    #region GetAppealAsync Tests

    [Test]
    public async Task GetAppealAsync_WithExistingAppeal_ReturnsAppeal()
    {
        var organizationId = Guid.NewGuid();
        var appealId = Guid.NewGuid();
        var appeal = CreateAppeal(organizationId, appealId: appealId);

        _appealRepository.GetByIdAsync(appealId).Returns(appeal);

        var result = await _service.GetAppealAsync(organizationId, appealId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(appealId));
    }

    [Test]
    public void GetAppealAsync_WithNonExistentAppeal_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var appealId = Guid.NewGuid();

        _appealRepository.GetByIdAsync(appealId).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.GetAppealAsync(organizationId, appealId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public void GetAppealAsync_WithAppealFromDifferentOrganization_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var differentOrgId = Guid.NewGuid();
        var appealId = Guid.NewGuid();
        var appeal = CreateAppeal(differentOrgId, appealId: appealId);

        _appealRepository.GetByIdAsync(appealId).Returns(appeal);

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.GetAppealAsync(organizationId, appealId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion

    #region GetAllAppealsAsync Tests

    [Test]
    public async Task GetAllAppealsAsync_ReturnsAllAppeals()
    {
        var organizationId = Guid.NewGuid();
        var appeals = new List<Appeal>
        {
            CreateAppeal(organizationId),
            CreateAppeal(organizationId)
        };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _appealRepository.GetAllAsync().Returns(appeals);

        var result = await _service.GetAllAppealsAsync(organizationId);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    #endregion

    #region GetAppealsByAssignmentIdAsync Tests

    [Test]
    public async Task GetAppealsByAssignmentIdAsync_ReturnsAppealsForAssignment()
    {
        var organizationId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var assignment = CreateAssignment(organizationId, assignmentId);
        var appeals = new List<Appeal> { CreateAppeal(organizationId, assignmentId) };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _assignmentService.GetAssignmentAsync(organizationId, assignmentId).Returns(assignment);
        _appealRepository.GetByAssignmentIdAsync(assignmentId).Returns(appeals);

        var result = await _service.GetAppealsByAssignmentIdAsync(organizationId, assignmentId);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].AssignmentId, Is.EqualTo(assignmentId));
    }

    #endregion

    #region GetAppealsByUserIdAsync Tests

    [Test]
    public async Task GetAppealsByUserIdAsync_ReturnsAppealsForUser()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var assignments = new List<Assignment> { CreateAssignment(organizationId, assignmentId) };
        var appeals = new List<Appeal> { CreateAppeal(organizationId, assignmentId) };

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _assignmentService.GetAllAssignmentsAsync(organizationId, userId: userId).Returns(assignments);
        _appealRepository.GetByAssignmentIdsAsync(Arg.Is<List<Guid>>(ids => ids.Contains(assignmentId)))
            .Returns(appeals);

        var result = await _service.GetAppealsByUserIdAsync(organizationId, userId);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].AssignmentId, Is.EqualTo(assignmentId));
    }

    [Test]
    public async Task GetAppealsByUserIdAsync_WithNoAssignments_ReturnsEmptyList()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _validationService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _assignmentService.GetAllAssignmentsAsync(organizationId, userId: userId).Returns(new List<Assignment>());
        _appealRepository.GetByAssignmentIdsAsync(Arg.Any<List<Guid>>()).Returns(new List<Appeal>());

        var result = await _service.GetAppealsByUserIdAsync(organizationId, userId);

        Assert.That(result, Is.Empty);
    }

    #endregion

    #region UpdateAppealAsync Tests

    [Test]
    public async Task UpdateAppealAsync_WithValidData_UpdatesAppeal()
    {
        var organizationId = Guid.NewGuid();
        var appealId = Guid.NewGuid();
        var appeal = CreateAppeal(organizationId, appealId: appealId);

        _appealRepository.GetByIdAsync(appealId).Returns(appeal);

        await _service.UpdateAppealAsync(organizationId, appealId, "New Title", "New Description");

        await _appealRepository.Received(1).UpdateAsync(Arg.Is<Appeal>(a =>
            a.Title == "New Title" &&
            a.Description == "New Description"));
    }

    [Test]
    public void UpdateAppealAsync_WithNonExistentAppeal_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var appealId = Guid.NewGuid();

        _appealRepository.GetByIdAsync(appealId).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.UpdateAppealAsync(organizationId, appealId, "New Title", "New Description"));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public void UpdateAppealAsync_WithAppealFromDifferentOrganization_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var differentOrgId = Guid.NewGuid();
        var appealId = Guid.NewGuid();
        var appeal = CreateAppeal(differentOrgId, appealId: appealId);

        _appealRepository.GetByIdAsync(appealId).Returns(appeal);

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.UpdateAppealAsync(organizationId, appealId, "New Title", "New Description"));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion

    #region DeleteAppealAsync Tests

    [Test]
    public async Task DeleteAppealAsync_WithExistingAppeal_DeletesAppeal()
    {
        var organizationId = Guid.NewGuid();
        var appealId = Guid.NewGuid();
        var appeal = CreateAppeal(organizationId, appealId: appealId);

        _appealRepository.GetByIdAsync(appealId).Returns(appeal);

        await _service.DeleteAppealAsync(organizationId, appealId);

        await _appealRepository.Received(1).DeleteAsync(appeal);
    }

    [Test]
    public void DeleteAppealAsync_WithNonExistentAppeal_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var appealId = Guid.NewGuid();

        _appealRepository.GetByIdAsync(appealId).ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.DeleteAppealAsync(organizationId, appealId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public void DeleteAppealAsync_WithAppealFromDifferentOrganization_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var differentOrgId = Guid.NewGuid();
        var appealId = Guid.NewGuid();
        var appeal = CreateAppeal(differentOrgId, appealId: appealId);

        _appealRepository.GetByIdAsync(appealId).Returns(appeal);

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.DeleteAppealAsync(organizationId, appealId));

        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    #endregion
}
