using Chronos.Data.Context;
using Chronos.Data.Repositories.Management;
using Chronos.Data.Repositories.Resources;
using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Management;
using Chronos.Offboarding.Removers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Chronos.Tests.Offboarding.Removers;

[TestFixture]
public class DepartmentRemoverTests
{
    private AppDbContext _context = null!;
    private ILogger<DepartmentRemover> _logger = null!;
    private IDepartmentRepository _departmentRepository = null!;
    private IAssignmentRepository _assignmentRepository = null!;
    private IActivityConstraintRepository _activityConstraintRepository = null!;
    private IActivityRepository _activityRepository = null!;
    private ISubjectRepository _subjectRepository = null!;
    private IRoleAssignmentRepository _roleAssignmentRepository = null!;
    private DepartmentRemover _remover = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new AppDbContext(options, null);

        _logger = Substitute.For<ILogger<DepartmentRemover>>();
        _departmentRepository = Substitute.For<IDepartmentRepository>();
        _assignmentRepository = Substitute.For<IAssignmentRepository>();
        _activityConstraintRepository = Substitute.For<IActivityConstraintRepository>();
        _activityRepository = Substitute.For<IActivityRepository>();
        _subjectRepository = Substitute.For<ISubjectRepository>();
        _roleAssignmentRepository = Substitute.For<IRoleAssignmentRepository>();

        _remover = new DepartmentRemover(
            _context,
            _logger,
            _departmentRepository,
            _assignmentRepository,
            _activityConstraintRepository,
            _activityRepository,
            _subjectRepository,
            _roleAssignmentRepository);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    private void SetupAllReposReturnZero(Guid deptId)
    {
        _assignmentRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(0);
        _activityConstraintRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(0);
        _activityRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(0);
        _subjectRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(0);
        _roleAssignmentRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(0);
    }

    #region RemoveAsync Tests

    [Test]
    public async Task RemoveAsync_DeletesAllRelatedEntitiesAndDepartment()
    {
        var deptId = Guid.NewGuid();
        var department = new Department
        {
            Id = deptId,
            OrganizationId = Guid.NewGuid(),
            Name = "Test Department"
        };

        SetupAllReposReturnZero(deptId);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(department);

        await _remover.RemoveAsync(deptId);

        await _assignmentRepository.Received(1).DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>());
        await _activityConstraintRepository.Received(1).DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>());
        await _activityRepository.Received(1).DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>());
        await _subjectRepository.Received(1).DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>());
        await _roleAssignmentRepository.Received(1).DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>());
        await _departmentRepository.Received(1).DeleteAsync(department, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RemoveAsync_ReturnsCorrectTotalDeletedCount()
    {
        var deptId = Guid.NewGuid();
        var department = new Department
        {
            Id = deptId,
            OrganizationId = Guid.NewGuid(),
            Name = "Test Department"
        };

        _assignmentRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(5);
        _activityConstraintRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(3);
        _activityRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(10);
        _subjectRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(4);
        _roleAssignmentRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(2);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(department);

        var result = await _remover.RemoveAsync(deptId);

        // 5+3+10+4+2 = 24, plus 1 for the department itself = 25
        Assert.That(result, Is.EqualTo(25));
    }

    [Test]
    public async Task RemoveAsync_WithEmptyDepartment_ReturnsOne()
    {
        var deptId = Guid.NewGuid();
        var department = new Department
        {
            Id = deptId,
            OrganizationId = Guid.NewGuid(),
            Name = "Empty Department"
        };

        SetupAllReposReturnZero(deptId);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(department);

        var result = await _remover.RemoveAsync(deptId);

        Assert.That(result, Is.EqualTo(1));
    }

    [Test]
    public void RemoveAsync_RollsBackAndRethrows_OnRepositoryFailure()
    {
        var deptId = Guid.NewGuid();

        _assignmentRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(0);
        _activityConstraintRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(0);
        _activityRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Database connection lost"));

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _remover.RemoveAsync(deptId));

        Assert.That(ex!.Message, Is.EqualTo("Database connection lost"));
    }

    [Test]
    public async Task RemoveAsync_MidwayFailure_DoesNotCallSubsequentRepos()
    {
        var deptId = Guid.NewGuid();

        _assignmentRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(0);
        _activityConstraintRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("fail"));

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _remover.RemoveAsync(deptId));

        // Repos after activityConstraint should not be called
        await _activityRepository.DidNotReceive().DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>());
        await _subjectRepository.DidNotReceive().DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>());
        await _roleAssignmentRepository.DidNotReceive().DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>());
        await _departmentRepository.DidNotReceive().GetByIdAsync(deptId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RemoveAsync_PassesDepartmentIdToAllRepos()
    {
        var deptId = Guid.NewGuid();
        var otherDeptId = Guid.NewGuid();
        var department = new Department
        {
            Id = deptId,
            OrganizationId = Guid.NewGuid(),
            Name = "Test Department"
        };

        SetupAllReposReturnZero(deptId);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(department);

        await _remover.RemoveAsync(deptId);

        // Verify no repo was called with a different ID
        await _assignmentRepository.DidNotReceive().DeleteAllByDepartmentIdAsync(otherDeptId, Arg.Any<CancellationToken>());
        await _activityRepository.DidNotReceive().DeleteAllByDepartmentIdAsync(otherDeptId, Arg.Any<CancellationToken>());
        await _subjectRepository.DidNotReceive().DeleteAllByDepartmentIdAsync(otherDeptId, Arg.Any<CancellationToken>());
    }

    #endregion
}
