using Chronos.Data.Repositories.Resources;
using Chronos.Domain.Resources;
using Chronos.MainApi.Resources.Services;
using Chronos.MainApi.Shared.ExternalMangement;
using Chronos.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Chronos.Tests.MainApi.Services.Resources;

[TestFixture]
public class ResourceAttributeAssignmentServiceTests
{
    private IResourceAttributeAssignmentRepository _resourceAttributeAssignmentRepository = null!;
    private ResourceValidationService _validationService = null!;
    private ILogger<ResourceAttributeAssignmentService> _logger = null!;
    private ResourceAttributeAssignmentService _service = null!;

    private IManagementExternalService _managementExternalService = null!;
    private ISubjectRepository _subjectRepository = null!;
    private IActivityRepository _activityRepository = null!;
    private IResourceRepository _resourceRepository = null!;
    private IResourceTypeRepository _resourceTypeRepository = null!;
    private IResourceAttributeRepository _resourceAttributeRepository = null!;
    private ILogger<ResourceValidationService> _validationLogger = null!;

    [SetUp]
    public void SetUp()
    {
        _resourceAttributeAssignmentRepository = Substitute.For<IResourceAttributeAssignmentRepository>();
        _managementExternalService = Substitute.For<IManagementExternalService>();
        _subjectRepository = Substitute.For<ISubjectRepository>();
        _activityRepository = Substitute.For<IActivityRepository>();
        _resourceRepository = Substitute.For<IResourceRepository>();
        _resourceTypeRepository = Substitute.For<IResourceTypeRepository>();
        _resourceAttributeRepository = Substitute.For<IResourceAttributeRepository>();
        _validationLogger = Substitute.For<ILogger<ResourceValidationService>>();
        _logger = Substitute.For<ILogger<ResourceAttributeAssignmentService>>();

        _validationService = new ResourceValidationService(
            _managementExternalService,
            _subjectRepository,
            _activityRepository,
            _resourceRepository,
            _resourceTypeRepository,
            _resourceAttributeRepository,
            _resourceAttributeAssignmentRepository,
            _validationLogger);

        _service = new ResourceAttributeAssignmentService(
            _resourceAttributeAssignmentRepository,
            _validationService,
            _logger);
    }

    #region CreateResourceAttributeAssignmentAsync Tests

    [Test]
    public async Task CreateResourceAttributeAssignmentAsync_WithExistingAssignment_ThrowsBadRequestAndDoesNotAdd()
    {
        var organizationId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var resourceAttributeId = Guid.NewGuid();

        _managementExternalService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _resourceAttributeAssignmentRepository.ExistsAsync(resourceId, resourceAttributeId).Returns(true);

        var ex = Assert.ThrowsAsync<BadRequestException>(async () =>
            await _service.CreateResourceAttributeAssignmentAsync(resourceId, resourceAttributeId, organizationId));

        Assert.That(ex!.Message, Does.Contain("already exists"));
        await _resourceAttributeAssignmentRepository.DidNotReceive().AddAsync(Arg.Any<ResourceAttributeAssignment>());
    }

    [Test]
    public async Task CreateResourceAttributeAssignmentAsync_WithNoExistingAssignment_CreatesSuccessfully()
    {
        var organizationId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var resourceAttributeId = Guid.NewGuid();

        _managementExternalService.ValidateOrganizationAsync(organizationId).Returns(Task.CompletedTask);
        _resourceAttributeAssignmentRepository.ExistsAsync(resourceId, resourceAttributeId).Returns(false);

        var result = await _service.CreateResourceAttributeAssignmentAsync(resourceId, resourceAttributeId, organizationId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.ResourceId, Is.EqualTo(resourceId));
        Assert.That(result.ResourceAttributeId, Is.EqualTo(resourceAttributeId));
        Assert.That(result.OrganizationId, Is.EqualTo(organizationId));
        await _resourceAttributeAssignmentRepository.Received(1).AddAsync(
            Arg.Is<ResourceAttributeAssignment>(raa =>
                raa.ResourceId == resourceId &&
                raa.ResourceAttributeId == resourceAttributeId &&
                raa.OrganizationId == organizationId));
    }

    #endregion
}
