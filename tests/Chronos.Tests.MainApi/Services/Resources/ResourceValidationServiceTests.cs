using Chronos.Data.Repositories.Resources;
using Chronos.Domain.Resources;
using Chronos.MainApi.Resources.Services;
using Chronos.MainApi.Shared.ExternalMangement;
using Chronos.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Chronos.Tests.MainApi.Services.Resources;

[TestFixture]
[Category("Unit")]
public class ResourceValidationServiceTests
{
    private ISubjectRepository _subjectRepository = null!;
    private IActivityRepository _activityRepository = null!;
    private IResourceRepository _resourceRepository = null!;
    private IResourceTypeRepository _resourceTypeRepository = null!;
    private IResourceAttributeRepository _resourceAttributeRepository = null!;
    private IResourceAttributeAssignmentRepository _resourceAttributeAssignmentRepository = null!;
    private IManagementExternalService _managementExternalService = null!;
    private ResourceValidationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _subjectRepository = Substitute.For<ISubjectRepository>();
        _activityRepository = Substitute.For<IActivityRepository>();
        _resourceRepository = Substitute.For<IResourceRepository>();
        _resourceTypeRepository = Substitute.For<IResourceTypeRepository>();
        _resourceAttributeRepository = Substitute.For<IResourceAttributeRepository>();
        _resourceAttributeAssignmentRepository = Substitute.For<IResourceAttributeAssignmentRepository>();
        _managementExternalService = Substitute.For<IManagementExternalService>();

        _service = new ResourceValidationService(
            _managementExternalService,
            _subjectRepository,
            _activityRepository,
            _resourceRepository,
            _resourceTypeRepository,
            _resourceAttributeRepository,
            _resourceAttributeAssignmentRepository,
            Substitute.For<ILogger<ResourceValidationService>>());
    }

    [Test]
    public async Task GivenOrgId_WhenValidateOrganization_ThenDelegatesToManagementService()
    {
        var orgId = Guid.NewGuid();

        await _service.ValidationOrganizationAsync(orgId);

        await _managementExternalService.Received(1).ValidateOrganizationAsync(orgId);
    }

    [Test]
    public void GivenNullSubject_WhenValidateAndGetSubject_ThenThrowsNotFound()
    {
        _subjectRepository.GetByIdAsync(Arg.Any<Guid>()).ReturnsNull();

        Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ValidateAndGetSubjectAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Test]
    public void GivenSubjectFromDifferentOrg_WhenValidateAndGetSubject_ThenThrowsNotFound()
    {
        var subjectId = Guid.NewGuid();
        _subjectRepository.GetByIdAsync(subjectId)
            .Returns(new Subject { Id = subjectId, OrganizationId = Guid.NewGuid(), Code = "S1", Name = "Test" });

        Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ValidateAndGetSubjectAsync(Guid.NewGuid(), subjectId));
    }

    [Test]
    public async Task GivenValidSubject_WhenValidateAndGetSubject_ThenReturnsSubject()
    {
        var orgId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        _subjectRepository.GetByIdAsync(subjectId)
            .Returns(new Subject { Id = subjectId, OrganizationId = orgId, Code = "S1", Name = "Test" });

        var result = await _service.ValidateAndGetSubjectAsync(orgId, subjectId);

        Assert.That(result.Id, Is.EqualTo(subjectId));
    }

    [Test]
    public void GivenNullActivity_WhenValidateAndGetActivity_ThenThrowsNotFound()
    {
        _activityRepository.GetByIdAsync(Arg.Any<Guid>()).ReturnsNull();

        Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ValidateAndGetActivityAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Test]
    public void GivenActivityFromDifferentOrg_WhenValidateAndGetActivity_ThenThrowsNotFound()
    {
        var activityId = Guid.NewGuid();
        _activityRepository.GetByIdAsync(activityId)
            .Returns(new Activity { Id = activityId, OrganizationId = Guid.NewGuid(), ActivityType = "Lecture" });

        Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ValidateAndGetActivityAsync(Guid.NewGuid(), activityId));
    }

    [Test]
    public void GivenNullResourceType_WhenValidateAndGetResourceType_ThenThrowsNotFound()
    {
        _resourceTypeRepository.GetByIdAsync(Arg.Any<Guid>()).ReturnsNull();

        Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ValidateAndGetResourceTypeAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Test]
    public void GivenNullAssignment_WhenValidateAndGetAssignment_ThenThrowsNotFound()
    {
        _resourceAttributeAssignmentRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).ReturnsNull();

        Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ValidateAndGetResourceAttributeAssignmentAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
    }

    [Test]
    public async Task GivenValidAssignment_WhenValidateAndGetAssignment_ThenReturnsWithoutOrgCheck()
    {
        // Documents that assignment validation does NOT check organizationId
        var resourceId = Guid.NewGuid();
        var attrId = Guid.NewGuid();
        var assignment = new ResourceAttributeAssignment { ResourceId = resourceId, ResourceAttributeId = attrId };
        _resourceAttributeAssignmentRepository.GetByIdAsync(resourceId, attrId).Returns(assignment);

        var result = await _service.ValidateAndGetResourceAttributeAssignmentAsync(
            Guid.NewGuid(), resourceId, attrId);

        Assert.That(result, Is.EqualTo(assignment));
    }
}
