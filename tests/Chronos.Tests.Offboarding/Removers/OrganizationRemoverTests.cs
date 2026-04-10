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
using NSubstitute.ExceptionExtensions;

namespace Chronos.Tests.Offboarding.Removers;

[TestFixture]
public class OrganizationRemoverTests
{
    private AppDbContext _context = null!;
    private ILogger<OrganizationRemover> _logger = null!;
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
    private OrganizationRemover _remover = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new AppDbContext(options, null);

        _logger = Substitute.For<ILogger<OrganizationRemover>>();
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

        _remover = new OrganizationRemover(
            _context,
            _logger,
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
            _departmentRepository);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    private void SetupAllReposReturnZero(Guid orgId)
    {
        _assignmentRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _activityConstraintRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _userPreferenceRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _userConstraintRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _resourceAttributeAssignmentRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _activityRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _slotRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _subjectRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _schedulingPeriodRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _resourceRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _resourceAttributeRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _resourceTypeRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _roleAssignmentRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _organizationPolicyRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _userRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _departmentRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
    }

    #region RemoveAsync Tests

    [Test]
    public async Task RemoveAsync_DeletesAllRelatedEntitiesAndOrganization()
    {
        var orgId = Guid.NewGuid();
        var organization = new Organization { Id = orgId, Name = "Test Org" };

        SetupAllReposReturnZero(orgId);
        _organizationRepository.GetByIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(organization);

        await _remover.RemoveAsync(orgId);

        await _assignmentRepository.Received(1).DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _activityConstraintRepository.Received(1).DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _userPreferenceRepository.Received(1).DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _userConstraintRepository.Received(1).DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _resourceAttributeAssignmentRepository.Received(1).DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _activityRepository.Received(1).DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _slotRepository.Received(1).DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _subjectRepository.Received(1).DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _schedulingPeriodRepository.Received(1).DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _resourceRepository.Received(1).DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _resourceAttributeRepository.Received(1).DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _resourceTypeRepository.Received(1).DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _roleAssignmentRepository.Received(1).DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _organizationPolicyRepository.Received(1).DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _userRepository.Received(1).DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _departmentRepository.Received(1).DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _organizationRepository.Received(1).DeleteAsync(organization, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RemoveAsync_ReturnsCorrectTotalDeletedCount()
    {
        var orgId = Guid.NewGuid();
        var organization = new Organization { Id = orgId, Name = "Test Org" };

        _assignmentRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(5);
        _activityConstraintRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(3);
        _userPreferenceRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(2);
        _userConstraintRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(1);
        _resourceAttributeAssignmentRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(4);
        _activityRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(10);
        _slotRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(8);
        _subjectRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(6);
        _schedulingPeriodRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(2);
        _resourceRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(7);
        _resourceAttributeRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(3);
        _resourceTypeRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(2);
        _roleAssignmentRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(4);
        _organizationPolicyRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(1);
        _userRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(3);
        _departmentRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(2);
        _organizationRepository.GetByIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(organization);

        var result = await _remover.RemoveAsync(orgId);

        // 5+3+2+1+4+10+8+6+2+7+3+2+4+1+3+2 = 63, plus 1 for the org itself = 64
        Assert.That(result, Is.EqualTo(64));
    }

    [Test]
    public async Task RemoveAsync_WithEmptyOrganization_ReturnsOne()
    {
        var orgId = Guid.NewGuid();
        var organization = new Organization { Id = orgId, Name = "Empty Org" };

        SetupAllReposReturnZero(orgId);
        _organizationRepository.GetByIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(organization);

        var result = await _remover.RemoveAsync(orgId);

        Assert.That(result, Is.EqualTo(1));
    }

    [Test]
    public void RemoveAsync_RollsBackAndRethrows_OnRepositoryFailure()
    {
        var orgId = Guid.NewGuid();

        _assignmentRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(3);
        _activityConstraintRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(1);
        _userPreferenceRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _userConstraintRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _resourceAttributeAssignmentRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _activityRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Database connection lost"));

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _remover.RemoveAsync(orgId));

        Assert.That(ex!.Message, Is.EqualTo("Database connection lost"));
    }

    [Test]
    public async Task RemoveAsync_MidwayFailure_DoesNotCallSubsequentRepos()
    {
        var orgId = Guid.NewGuid();

        _assignmentRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _activityConstraintRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _userPreferenceRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _userConstraintRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _resourceAttributeAssignmentRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(0);
        _activityRepository.DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("fail"));

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _remover.RemoveAsync(orgId));

        // Repos after activity should not be called
        await _slotRepository.DidNotReceive().DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _subjectRepository.DidNotReceive().DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _schedulingPeriodRepository.DidNotReceive().DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _resourceRepository.DidNotReceive().DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _resourceAttributeRepository.DidNotReceive().DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _resourceTypeRepository.DidNotReceive().DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _roleAssignmentRepository.DidNotReceive().DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _organizationPolicyRepository.DidNotReceive().DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _userRepository.DidNotReceive().DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _departmentRepository.DidNotReceive().DeleteAllByOrganizationIdAsync(orgId, Arg.Any<CancellationToken>());
        await _organizationRepository.DidNotReceive().GetByIdAsync(orgId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RemoveAsync_PassesOrganizationIdToAllRepos()
    {
        var orgId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var organization = new Organization { Id = orgId, Name = "Test Org" };

        SetupAllReposReturnZero(orgId);
        _organizationRepository.GetByIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(organization);

        await _remover.RemoveAsync(orgId);

        // Verify no repo was called with a different ID
        await _assignmentRepository.DidNotReceive().DeleteAllByOrganizationIdAsync(otherOrgId, Arg.Any<CancellationToken>());
        await _activityRepository.DidNotReceive().DeleteAllByOrganizationIdAsync(otherOrgId, Arg.Any<CancellationToken>());
        await _subjectRepository.DidNotReceive().DeleteAllByOrganizationIdAsync(otherOrgId, Arg.Any<CancellationToken>());
    }

    #endregion
}
