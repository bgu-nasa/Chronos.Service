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
public class ActivityServiceTests
{
    private IActivityRepository _activityRepository;
    private ISubjectService _subjectService;
    private ResourceValidationService _validationService;
    private ActivityService _sut;

    private IManagementExternalService _managementExternalService;
    private ISubjectRepository _subjectRepository;
    private IActivityRepository _validationActivityRepo;
    private IResourceRepository _resourceRepository;
    private IResourceTypeRepository _resourceTypeRepository;
    private IResourceAttributeRepository _resourceAttributeRepository;
    private IResourceAttributeAssignmentRepository _resourceAttributeAssignmentRepository;

    private readonly Guid _orgId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _activityRepository = Substitute.For<IActivityRepository>();
        _subjectService = Substitute.For<ISubjectService>();

        _managementExternalService = Substitute.For<IManagementExternalService>();
        _subjectRepository = Substitute.For<ISubjectRepository>();
        _validationActivityRepo = Substitute.For<IActivityRepository>();
        _resourceRepository = Substitute.For<IResourceRepository>();
        _resourceTypeRepository = Substitute.For<IResourceTypeRepository>();
        _resourceAttributeRepository = Substitute.For<IResourceAttributeRepository>();
        _resourceAttributeAssignmentRepository = Substitute.For<IResourceAttributeAssignmentRepository>();

        _validationService = new ResourceValidationService(
            _managementExternalService,
            _subjectRepository,
            _validationActivityRepo,
            _resourceRepository,
            _resourceTypeRepository,
            _resourceAttributeRepository,
            _resourceAttributeAssignmentRepository,
            Substitute.For<ILogger<ResourceValidationService>>());

        _sut = new ActivityService(
            _activityRepository,
            _validationService,
            _subjectService,
            Substitute.For<ILogger<ActivityService>>());
    }

    #region CreateActivityAsync

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(-99)]
    public void GivenInvalidDuration_WhenCreateActivity_ThenThrowsArgumentException(int duration)
    {
        Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.CreateActivityAsync(_orgId, Guid.NewGuid(), Guid.NewGuid(), "Lecture", 30, duration));
    }

    [Test]
    public void GivenSubjectNotFound_WhenCreateActivity_ThenThrows()
    {
        var subjectId = Guid.NewGuid();
        _subjectService.GetSubjectAsync(_orgId, subjectId)
            .Returns(Task.FromResult<Subject>(null!));

        Assert.ThrowsAsync<Exception>(() =>
            _sut.CreateActivityAsync(_orgId, subjectId, Guid.NewGuid(), "Lecture", 30, 2));
    }

    [Test]
    public async Task GivenValidInput_WhenCreateActivity_ThenAddsToRepository()
    {
        var subjectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _subjectService.GetSubjectAsync(_orgId, subjectId)
            .Returns(MakeSubject(subjectId, _orgId));

        var result = await _sut.CreateActivityAsync(_orgId, subjectId, userId, "Lab", 25, 3);

        Assert.Multiple(() =>
        {
            Assert.That(result.OrganizationId, Is.EqualTo(_orgId));
            Assert.That(result.SubjectId, Is.EqualTo(subjectId));
            Assert.That(result.ActivityType, Is.EqualTo("Lab"));
            Assert.That(result.Duration, Is.EqualTo(3));
        });
        await _activityRepository.Received(1).AddAsync(Arg.Any<Activity>());
    }

    #endregion

    #region GetActivityAsync

    [Test]
    public void GivenActivityNotInOrg_WhenGetActivity_ThenThrowsNotFound()
    {
        var activityId = Guid.NewGuid();
        _validationActivityRepo.GetByIdAsync(activityId)
            .Returns(MakeActivity(activityId, Guid.NewGuid()));

        Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.GetActivityAsync(_orgId, activityId));
    }

    [Test]
    public async Task GivenActivityExists_WhenGetActivity_ThenReturnsIt()
    {
        var activityId = Guid.NewGuid();
        var activity = MakeActivity(activityId, _orgId);
        _validationActivityRepo.GetByIdAsync(activityId).Returns(activity);

        var result = await _sut.GetActivityAsync(_orgId, activityId);

        Assert.That(result.Id, Is.EqualTo(activityId));
    }

    #endregion

    #region GetActivitiesAsync

    [Test]
    public async Task GivenMixedOrgActivities_WhenGetActivities_ThenFiltersToOrg()
    {
        var own = MakeActivity(Guid.NewGuid(), _orgId);
        var foreign = MakeActivity(Guid.NewGuid(), Guid.NewGuid());
        _activityRepository.GetAllAsync().Returns(new List<Activity> { own, foreign });

        var result = await _sut.GetActivitiesAsync(_orgId);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo(own.Id));
    }

    #endregion

    #region GetActivitiesBySubjectAsync

    [Test]
    public async Task GivenActivitiesForMultipleSubjects_WhenGetBySubject_ThenFiltersCorrectly()
    {
        var targetSubjectId = Guid.NewGuid();
        var match = MakeActivity(Guid.NewGuid(), _orgId, targetSubjectId);
        var otherSubject = MakeActivity(Guid.NewGuid(), _orgId, Guid.NewGuid());
        _activityRepository.GetAllAsync().Returns(new List<Activity> { match, otherSubject });

        var result = await _sut.GetActivitiesBySubjectAsync(_orgId, targetSubjectId);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].SubjectId, Is.EqualTo(targetSubjectId));
    }

    #endregion

    #region UpdateActivityAsync

    [Test]
    public void GivenZeroDuration_WhenUpdateActivity_ThenThrowsArgumentException()
    {
        Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.UpdateActivityAsync(_orgId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Lecture", 30, 0));
    }

    [Test]
    public async Task GivenValidUpdate_WhenUpdateActivity_ThenUpdatesFields()
    {
        var activityId = Guid.NewGuid();
        var activity = MakeActivity(activityId, _orgId);
        _validationActivityRepo.GetByIdAsync(activityId).Returns(activity);

        var newSubjectId = Guid.NewGuid();
        await _sut.UpdateActivityAsync(_orgId, activityId, newSubjectId, Guid.NewGuid(), "Seminar", 40, 4);

        await _activityRepository.Received(1).UpdateAsync(Arg.Is<Activity>(a =>
            a.ActivityType == "Seminar" && a.Duration == 4 && a.SubjectId == newSubjectId));
    }

    #endregion

    #region DeleteActivityAsync

    [Test]
    public async Task GivenExistingActivity_WhenDelete_ThenCallsRepository()
    {
        var activityId = Guid.NewGuid();
        var activity = MakeActivity(activityId, _orgId);
        _validationActivityRepo.GetByIdAsync(activityId).Returns(activity);

        await _sut.DeleteActivityAsync(_orgId, activityId);

        await _activityRepository.Received(1).DeleteAsync(activity);
    }

    #endregion

    #region Helpers

    private static Activity MakeActivity(Guid id, Guid orgId, Guid? subjectId = null)
    {
        return new Activity
        {
            Id = id,
            OrganizationId = orgId,
            SubjectId = subjectId ?? Guid.NewGuid(),
            AssignedUserId = Guid.NewGuid(),
            ActivityType = "Lecture",
            Duration = 2
        };
    }

    private static Subject MakeSubject(Guid id, Guid orgId)
    {
        return new Subject
        {
            Id = id,
            OrganizationId = orgId,
            DepartmentId = Guid.NewGuid(),
            SchedulingPeriodId = Guid.NewGuid(),
            Code = "CS101",
            Name = "Intro to CS"
        };
    }

    #endregion
}
