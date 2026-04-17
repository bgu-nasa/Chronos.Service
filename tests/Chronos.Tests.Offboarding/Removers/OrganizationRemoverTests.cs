using Chronos.Data.Context;
using Chronos.Data.Repositories.Auth;
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
public class OrganizationRemoverTests
{
    private AppDbContext _dbContext = null!;
    private OrganizationRemover _sut = null!;

    private IOrganizationRepository _organizationRepository = null!;
    private IAssignmentRepository _assignmentRepository = null!;
    private IActivityConstraintRepository _activityConstraintRepository = null!;
    private IUserPreferenceRepository _userPreferenceRepository = null!;
    private IUserConstraintRepository _userConstraintRepository = null!;
    private IResourceAttributeAssignmentRepository _resourceAttributeAssignmentRepository = null!;
    private IActivityRepository _activityRepository = null!;
    private ISlotRepository _slotRepository = null!;
    private ISubjectRepository _subjectRepository = null!;
    private ISchedulingPeriodRepository _schedulingPeriodRepository = null!;
    private IResourceRepository _resourceRepository = null!;
    private IResourceAttributeRepository _resourceAttributeRepository = null!;
    private IResourceTypeRepository _resourceTypeRepository = null!;
    private IRoleAssignmentRepository _roleAssignmentRepository = null!;
    private IOrganizationPolicyRepository _organizationPolicyRepository = null!;
    private IUserRepository _userRepository = null!;
    private IDepartmentRepository _departmentRepository = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new AppDbContext(options, null);

        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _assignmentRepository = Substitute.For<IAssignmentRepository>();
        _activityConstraintRepository = Substitute.For<IActivityConstraintRepository>();
        _userPreferenceRepository = Substitute.For<IUserPreferenceRepository>();
        _userConstraintRepository = Substitute.For<IUserConstraintRepository>();
        _resourceAttributeAssignmentRepository = Substitute.For<IResourceAttributeAssignmentRepository>();
        _activityRepository = Substitute.For<IActivityRepository>();
        _slotRepository = Substitute.For<ISlotRepository>();
        _subjectRepository = Substitute.For<ISubjectRepository>();
        _schedulingPeriodRepository = Substitute.For<ISchedulingPeriodRepository>();
        _resourceRepository = Substitute.For<IResourceRepository>();
        _resourceAttributeRepository = Substitute.For<IResourceAttributeRepository>();
        _resourceTypeRepository = Substitute.For<IResourceTypeRepository>();
        _roleAssignmentRepository = Substitute.For<IRoleAssignmentRepository>();
        _organizationPolicyRepository = Substitute.For<IOrganizationPolicyRepository>();
        _userRepository = Substitute.For<IUserRepository>();
        _departmentRepository = Substitute.For<IDepartmentRepository>();

        _sut = new OrganizationRemover(
            _dbContext,
            Substitute.For<ILogger<OrganizationRemover>>(),
            _organizationRepository,
            _assignmentRepository,
            _activityConstraintRepository,
            _userPreferenceRepository,
            _userConstraintRepository,
            _resourceAttributeAssignmentRepository,
            _activityRepository,
            _slotRepository,
            _subjectRepository,
            _schedulingPeriodRepository,
            _resourceRepository,
            _resourceAttributeRepository,
            _resourceTypeRepository,
            _roleAssignmentRepository,
            _organizationPolicyRepository,
            _userRepository,
            _departmentRepository
        );
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    [Test]
    public async Task GivenOrganizationWithData_WhenRemove_ThenDeletesAllRelatedEntitiesInOrder()
    {
        var orgId = Guid.NewGuid();
        var org = new Organization { Id = orgId, Name = "TestOrg" };

        SetupAllDeleteReturns(orgId, deletedPerTable: 3);
        _organizationRepository.GetByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(org);

        var result = await _sut.RemoveAsync(orgId);

        // 16 tables × 3 rows + 1 for the org itself
        Assert.That(result, Is.EqualTo(49));
    }

    [Test]
    public async Task GivenOrganizationWithData_WhenRemove_ThenCallsDeletesInCorrectDependencyOrder()
    {
        var orgId = Guid.NewGuid();
        var callOrder = new List<string>();

        _assignmentRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(ci => { callOrder.Add("Assignments"); return 0; });
        _activityConstraintRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(ci => { callOrder.Add("ActivityConstraints"); return 0; });
        _userPreferenceRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(ci => { callOrder.Add("UserPreferences"); return 0; });
        _userConstraintRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(ci => { callOrder.Add("UserConstraints"); return 0; });
        _resourceAttributeAssignmentRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(ci => { callOrder.Add("ResourceAttributeAssignments"); return 0; });
        _activityRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(ci => { callOrder.Add("Activities"); return 0; });
        _slotRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(ci => { callOrder.Add("Slots"); return 0; });
        _subjectRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(ci => { callOrder.Add("Subjects"); return 0; });
        _schedulingPeriodRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(ci => { callOrder.Add("SchedulingPeriods"); return 0; });
        _resourceRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(ci => { callOrder.Add("Resources"); return 0; });
        _resourceAttributeRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(ci => { callOrder.Add("ResourceAttributes"); return 0; });
        _resourceTypeRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(ci => { callOrder.Add("ResourceTypes"); return 0; });
        _roleAssignmentRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(ci => { callOrder.Add("RoleAssignments"); return 0; });
        _organizationPolicyRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(ci => { callOrder.Add("OrganizationPolicies"); return 0; });
        _userRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(ci => { callOrder.Add("Users"); return 0; });
        _departmentRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(ci => { callOrder.Add("Departments"); return 0; });
        _organizationRepository.GetByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns((Organization?)null);

        await _sut.RemoveAsync(orgId);

        // Leaf entities (assignments, constraints) must be deleted before parents (activities, subjects, org)
        Assert.That(callOrder.IndexOf("Assignments"), Is.LessThan(callOrder.IndexOf("Activities")));
        Assert.That(callOrder.IndexOf("Activities"), Is.LessThan(callOrder.IndexOf("Subjects")));
        Assert.That(callOrder.IndexOf("Subjects"), Is.LessThan(callOrder.IndexOf("Departments")));
        Assert.That(callOrder.IndexOf("Users"), Is.LessThan(callOrder.IndexOf("Departments")));
    }

    [Test]
    public async Task GivenOrganizationNotFoundAtEnd_WhenRemove_ThenSkipsOrgDeleteAndStillCommits()
    {
        var orgId = Guid.NewGuid();
        SetupAllDeleteReturns(orgId, deletedPerTable: 0);
        _organizationRepository.GetByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns((Organization?)null);

        var result = await _sut.RemoveAsync(orgId);

        Assert.That(result, Is.EqualTo(0));
        await _organizationRepository.DidNotReceive()
            .DeleteAsync(Arg.Any<Organization>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GivenOrganizationFound_WhenRemove_ThenHardDeletesOrganization()
    {
        var orgId = Guid.NewGuid();
        var org = new Organization { Id = orgId, Name = "ToDelete" };
        SetupAllDeleteReturns(orgId, deletedPerTable: 0);
        _organizationRepository.GetByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(org);

        var result = await _sut.RemoveAsync(orgId);

        Assert.That(result, Is.EqualTo(1));
        await _organizationRepository.Received(1)
            .DeleteAsync(org, Arg.Any<CancellationToken>());
    }

    [Test]
    public void GivenRepositoryThrows_WhenRemove_ThenPropagatesException()
    {
        var orgId = Guid.NewGuid();
        _assignmentRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new InvalidOperationException("DB error"));

        Assert.ThrowsAsync<InvalidOperationException>(() => _sut.RemoveAsync(orgId));
    }

    private void SetupAllDeleteReturns(Guid orgId, int deletedPerTable)
    {
        _assignmentRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(deletedPerTable);
        _activityConstraintRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(deletedPerTable);
        _userPreferenceRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(deletedPerTable);
        _userConstraintRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(deletedPerTable);
        _resourceAttributeAssignmentRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(deletedPerTable);
        _activityRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(deletedPerTable);
        _slotRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(deletedPerTable);
        _subjectRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(deletedPerTable);
        _schedulingPeriodRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(deletedPerTable);
        _resourceRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(deletedPerTable);
        _resourceAttributeRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(deletedPerTable);
        _resourceTypeRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(deletedPerTable);
        _roleAssignmentRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(deletedPerTable);
        _organizationPolicyRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(deletedPerTable);
        _userRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(deletedPerTable);
        _departmentRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(deletedPerTable);
    }
}
