using Chronos.Data.Repositories.Resources;
using Chronos.Domain.Resources;
using Chronos.MainApi.Resources.Services;
using Chronos.MainApi.Shared.ExternalMangement;
using Chronos.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Chronos.Tests.MainApi.Services.Resources;

[TestFixture]
[Category("Unit")]
public class SubjectServiceTests
{
    private ISubjectRepository _subjectRepository;
    private IExternalSchedulingPeriodService _externalSchedulingPeriodService;
    private IExternalDepartmentService _externalDepartmentService;
    private ResourceValidationService _validationService;
    private SubjectService _sut;

    private IManagementExternalService _managementExternalService;

    private readonly Guid _orgId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _subjectRepository = Substitute.For<ISubjectRepository>();
        _externalSchedulingPeriodService = Substitute.For<IExternalSchedulingPeriodService>();
        _externalDepartmentService = Substitute.For<IExternalDepartmentService>();
        _managementExternalService = Substitute.For<IManagementExternalService>();

        _validationService = new ResourceValidationService(
            _managementExternalService,
            _subjectRepository,
            Substitute.For<IActivityRepository>(),
            Substitute.For<IResourceRepository>(),
            Substitute.For<IResourceTypeRepository>(),
            Substitute.For<IResourceAttributeRepository>(),
            Substitute.For<IResourceAttributeAssignmentRepository>(),
            Substitute.For<ILogger<ResourceValidationService>>());

        _sut = new SubjectService(
            _subjectRepository,
            _validationService,
            _externalSchedulingPeriodService,
            _externalDepartmentService,
            Substitute.For<ILogger<SubjectService>>());
    }

    #region CreateSubjectAsync

    [Test]
    public void GivenInvalidSchedulingPeriod_WhenCreateSubject_ThenThrows()
    {
        var spId = Guid.NewGuid();
        _externalSchedulingPeriodService
            .validateSchedulingPeriodAsync(_orgId, spId)
            .Returns(Task.FromException(new NotFoundException("Scheduling period not found")));

        Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.CreateSubjectAsync(_orgId, Guid.NewGuid(), spId, "CS101", "Intro"));
    }

    [Test]
    public void GivenInvalidDepartment_WhenCreateSubject_ThenThrows()
    {
        var deptId = Guid.NewGuid();
        _externalDepartmentService
            .validateDepartmentAsync(_orgId, deptId)
            .Returns(Task.FromException(new NotFoundException("Department not found")));

        Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.CreateSubjectAsync(_orgId, deptId, Guid.NewGuid(), "CS101", "Intro"));
    }

    [Test]
    public async Task GivenValidInput_WhenCreateSubject_ThenAddsToRepository()
    {
        var deptId = Guid.NewGuid();
        var spId = Guid.NewGuid();

        var result = await _sut.CreateSubjectAsync(_orgId, deptId, spId, "MATH201", "Linear Algebra");

        Assert.Multiple(() =>
        {
            Assert.That(result.OrganizationId, Is.EqualTo(_orgId));
            Assert.That(result.DepartmentId, Is.EqualTo(deptId));
            Assert.That(result.Code, Is.EqualTo("MATH201"));
            Assert.That(result.Name, Is.EqualTo("Linear Algebra"));
        });
        await _subjectRepository.Received(1).AddAsync(Arg.Any<Subject>());
    }

    #endregion

    #region GetSubjectAsync

    [Test]
    public void GivenSubjectInDifferentOrg_WhenGetSubject_ThenThrowsNotFound()
    {
        var subjectId = Guid.NewGuid();
        _subjectRepository.GetByIdAsync(subjectId)
            .Returns(MakeSubject(subjectId, Guid.NewGuid()));

        Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.GetSubjectAsync(_orgId, subjectId));
    }

    [Test]
    public async Task GivenSubjectExists_WhenGetSubject_ThenReturnsIt()
    {
        var subjectId = Guid.NewGuid();
        var subject = MakeSubject(subjectId, _orgId);
        _subjectRepository.GetByIdAsync(subjectId).Returns(subject);

        var result = await _sut.GetSubjectAsync(_orgId, subjectId);

        Assert.That(result.Id, Is.EqualTo(subjectId));
    }

    #endregion

    #region GetSubjectsAsync

    [Test]
    public async Task GivenMixedOrgSubjects_WhenGetSubjects_ThenFiltersToOrg()
    {
        var own = MakeSubject(Guid.NewGuid(), _orgId);
        var foreign = MakeSubject(Guid.NewGuid(), Guid.NewGuid());
        _subjectRepository.GetAllAsync().Returns(new List<Subject> { own, foreign });

        var result = await _sut.GetSubjectsAsync(_orgId);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo(own.Id));
    }

    #endregion

    #region GetSubjectsByDepartmentAsync

    [Test]
    public async Task GivenSubjectsAcrossDepartments_WhenGetByDepartment_ThenFiltersBoth()
    {
        var targetDeptId = Guid.NewGuid();
        var match = MakeSubject(Guid.NewGuid(), _orgId, targetDeptId);
        var otherDept = MakeSubject(Guid.NewGuid(), _orgId, Guid.NewGuid());
        var foreignOrg = MakeSubject(Guid.NewGuid(), Guid.NewGuid(), targetDeptId);
        _subjectRepository.GetAllAsync().Returns(new List<Subject> { match, otherDept, foreignOrg });

        var result = await _sut.GetSubjectsByDepartmentAsync(_orgId, targetDeptId);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].DepartmentId, Is.EqualTo(targetDeptId));
    }

    #endregion

    #region UpdateSubjectAsync

    [Test]
    public async Task GivenValidUpdate_WhenUpdateSubject_ThenUpdatesFields()
    {
        var subjectId = Guid.NewGuid();
        var subject = MakeSubject(subjectId, _orgId);
        _subjectRepository.GetByIdAsync(subjectId).Returns(subject);

        var newDeptId = Guid.NewGuid();
        var newSpId = Guid.NewGuid();
        await _sut.UpdateSubjectAsync(_orgId, subjectId, newDeptId, newSpId, "PHY101", "Physics");

        await _subjectRepository.Received(1).UpdateAsync(Arg.Is<Subject>(s =>
            s.Code == "PHY101" && s.Name == "Physics" && s.DepartmentId == newDeptId));
    }

    [Test]
    public void GivenInvalidDepartmentOnUpdate_WhenUpdateSubject_ThenThrows()
    {
        var subjectId = Guid.NewGuid();
        _subjectRepository.GetByIdAsync(subjectId).Returns(MakeSubject(subjectId, _orgId));

        var badDeptId = Guid.NewGuid();
        _externalDepartmentService.validateDepartmentAsync(_orgId, badDeptId)
            .Returns(Task.FromException(new NotFoundException("Department not found")));

        Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.UpdateSubjectAsync(_orgId, subjectId, badDeptId, Guid.NewGuid(), "X", "Y"));
    }

    #endregion

    #region DeleteSubjectAsync

    [Test]
    public async Task GivenExistingSubject_WhenDelete_ThenCallsRepository()
    {
        var subjectId = Guid.NewGuid();
        var subject = MakeSubject(subjectId, _orgId);
        _subjectRepository.GetByIdAsync(subjectId).Returns(subject);

        await _sut.DeleteSubjectAsync(_orgId, subjectId);

        await _subjectRepository.Received(1).DeleteAsync(subject);
    }

    #endregion

    #region Helpers

    private static Subject MakeSubject(Guid id, Guid orgId, Guid? deptId = null)
    {
        return new Subject
        {
            Id = id,
            OrganizationId = orgId,
            DepartmentId = deptId ?? Guid.NewGuid(),
            SchedulingPeriodId = Guid.NewGuid(),
            Code = "CS101",
            Name = "Test Subject"
        };
    }

    #endregion
}
