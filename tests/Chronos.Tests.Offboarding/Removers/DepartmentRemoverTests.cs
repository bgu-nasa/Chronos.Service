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

namespace Chronos.Tests.Offboarding.Removers;

[TestFixture]
[Category("Unit")]
public class DepartmentRemoverTests
{
    private AppDbContext _dbContext = null!;
    private DepartmentRemover _sut = null!;

    private IDepartmentRepository _departmentRepository = null!;
    private IAssignmentRepository _assignmentRepository = null!;
    private IActivityConstraintRepository _activityConstraintRepository = null!;
    private IActivityRepository _activityRepository = null!;
    private ISubjectRepository _subjectRepository = null!;
    private IRoleAssignmentRepository _roleAssignmentRepository = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new AppDbContext(options, null);

        _departmentRepository = Substitute.For<IDepartmentRepository>();
        _assignmentRepository = Substitute.For<IAssignmentRepository>();
        _activityConstraintRepository = Substitute.For<IActivityConstraintRepository>();
        _activityRepository = Substitute.For<IActivityRepository>();
        _subjectRepository = Substitute.For<ISubjectRepository>();
        _roleAssignmentRepository = Substitute.For<IRoleAssignmentRepository>();

        _sut = new DepartmentRemover(
            _dbContext,
            Substitute.For<ILogger<DepartmentRemover>>(),
            _departmentRepository,
            _assignmentRepository,
            _activityConstraintRepository,
            _activityRepository,
            _subjectRepository,
            _roleAssignmentRepository
        );
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    [Test]
    public async Task GivenDepartmentWithData_WhenRemove_ThenDeletesAllRelatedEntities()
    {
        var deptId = Guid.NewGuid();
        var dept = new Department { Id = deptId, OrganizationId = Guid.NewGuid(), Name = "Engineering" };

        SetupAllDeleteReturns(deptId, deletedPerTable: 2);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>())
            .Returns(dept);

        var result = await _sut.RemoveAsync(deptId);

        // 5 tables × 2 rows + 1 for the department itself
        Assert.That(result, Is.EqualTo(11));
    }

    [Test]
    public async Task GivenDepartmentWithData_WhenRemove_ThenCallsDeletesInCorrectDependencyOrder()
    {
        var deptId = Guid.NewGuid();
        var callOrder = new List<string>();

        _assignmentRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>())
            .Returns(ci => { callOrder.Add("Assignments"); return 0; });
        _activityConstraintRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>())
            .Returns(ci => { callOrder.Add("ActivityConstraints"); return 0; });
        _activityRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>())
            .Returns(ci => { callOrder.Add("Activities"); return 0; });
        _subjectRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>())
            .Returns(ci => { callOrder.Add("Subjects"); return 0; });
        _roleAssignmentRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>())
            .Returns(ci => { callOrder.Add("RoleAssignments"); return 0; });
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>())
            .Returns((Department?)null);

        await _sut.RemoveAsync(deptId);

        Assert.That(callOrder.IndexOf("Assignments"), Is.LessThan(callOrder.IndexOf("Activities")));
        Assert.That(callOrder.IndexOf("ActivityConstraints"), Is.LessThan(callOrder.IndexOf("Activities")));
        Assert.That(callOrder.IndexOf("Activities"), Is.LessThan(callOrder.IndexOf("Subjects")));
    }

    [Test]
    public async Task GivenDepartmentNotFoundAtEnd_WhenRemove_ThenSkipsDeptDeleteAndStillCommits()
    {
        var deptId = Guid.NewGuid();
        SetupAllDeleteReturns(deptId, deletedPerTable: 0);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>())
            .Returns((Department?)null);

        var result = await _sut.RemoveAsync(deptId);

        Assert.That(result, Is.EqualTo(0));
        await _departmentRepository.DidNotReceive()
            .DeleteAsync(Arg.Any<Department>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GivenDepartmentFound_WhenRemove_ThenHardDeletesDepartment()
    {
        var deptId = Guid.NewGuid();
        var dept = new Department { Id = deptId, OrganizationId = Guid.NewGuid(), Name = "Sales" };
        SetupAllDeleteReturns(deptId, deletedPerTable: 0);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>())
            .Returns(dept);

        var result = await _sut.RemoveAsync(deptId);

        Assert.That(result, Is.EqualTo(1));
        await _departmentRepository.Received(1)
            .DeleteAsync(dept, Arg.Any<CancellationToken>());
    }

    [Test]
    public void GivenRepositoryThrows_WhenRemove_ThenPropagatesException()
    {
        var deptId = Guid.NewGuid();
        _assignmentRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new InvalidOperationException("FK violation"));

        Assert.ThrowsAsync<InvalidOperationException>(() => _sut.RemoveAsync(deptId));
    }

    private void SetupAllDeleteReturns(Guid deptId, int deletedPerTable)
    {
        _assignmentRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(deletedPerTable);
        _activityConstraintRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(deletedPerTable);
        _activityRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(deletedPerTable);
        _subjectRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(deletedPerTable);
        _roleAssignmentRepository.DeleteAllByDepartmentIdAsync(deptId, Arg.Any<CancellationToken>()).Returns(deletedPerTable);
    }
}
