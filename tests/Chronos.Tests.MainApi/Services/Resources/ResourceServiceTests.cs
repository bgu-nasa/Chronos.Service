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
public class ResourceServiceTests
{
    private IResourceRepository _resourceRepository;
    private ResourceValidationService _validationService;
    private ResourceService _sut;

    private readonly Guid _orgId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _resourceRepository = Substitute.For<IResourceRepository>();

        _validationService = new ResourceValidationService(
            Substitute.For<IManagementExternalService>(),
            Substitute.For<ISubjectRepository>(),
            Substitute.For<IActivityRepository>(),
            _resourceRepository,
            Substitute.For<IResourceTypeRepository>(),
            Substitute.For<IResourceAttributeRepository>(),
            Substitute.For<IResourceAttributeAssignmentRepository>(),
            Substitute.For<ILogger<ResourceValidationService>>());

        _sut = new ResourceService(
            _resourceRepository,
            _validationService,
            Substitute.For<ILogger<ResourceService>>());
    }

    [Test]
    public async Task GivenValidInput_WhenCreateResource_ThenAddsAndReturns()
    {
        var typeId = Guid.NewGuid();

        var result = await _sut.CreateResourceAsync(Guid.NewGuid(), _orgId, typeId, "Building A", "Room 101", 50);

        Assert.Multiple(() =>
        {
            Assert.That(result.OrganizationId, Is.EqualTo(_orgId));
            Assert.That(result.Location, Is.EqualTo("Building A"));
            Assert.That(result.Identifier, Is.EqualTo("Room 101"));
            Assert.That(result.Capacity, Is.EqualTo(50));
        });
        await _resourceRepository.Received(1).AddAsync(Arg.Any<Resource>());
    }

    [Test]
    public void GivenResourceInDifferentOrg_WhenGetResource_ThenThrowsNotFound()
    {
        var resourceId = Guid.NewGuid();
        _resourceRepository.GetByIdAsync(resourceId)
            .Returns(MakeResource(resourceId, Guid.NewGuid()));

        Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.GetResourceAsync(_orgId, resourceId));
    }

    [Test]
    public async Task GivenMixedOrgResources_WhenGetResources_ThenFiltersToOrg()
    {
        var own = MakeResource(Guid.NewGuid(), _orgId);
        var foreign = MakeResource(Guid.NewGuid(), Guid.NewGuid());
        _resourceRepository.GetAllAsync().Returns(new List<Resource> { own, foreign });

        var result = await _sut.GetResourcesAsync(_orgId);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo(own.Id));
    }

    [Test]
    public async Task GivenValidUpdate_WhenUpdateResource_ThenPersistsChanges()
    {
        var resourceId = Guid.NewGuid();
        _resourceRepository.GetByIdAsync(resourceId).Returns(MakeResource(resourceId, _orgId));

        var newTypeId = Guid.NewGuid();
        await _sut.UpdateResourceAsync(_orgId, resourceId, newTypeId, "Building B", "Lab 5", 30);

        await _resourceRepository.Received(1).UpdateAsync(Arg.Is<Resource>(r =>
            r.Location == "Building B" && r.Identifier == "Lab 5" && r.Capacity == 30));
    }

    [Test]
    public async Task GivenExistingResource_WhenDelete_ThenCallsRepository()
    {
        var resourceId = Guid.NewGuid();
        var resource = MakeResource(resourceId, _orgId);
        _resourceRepository.GetByIdAsync(resourceId).Returns(resource);

        await _sut.DeleteResourceAsync(_orgId, resourceId);

        await _resourceRepository.Received(1).DeleteAsync(resource);
    }

    private static Resource MakeResource(Guid id, Guid orgId)
    {
        return new Resource
        {
            Id = id,
            OrganizationId = orgId,
            ResourceTypeId = Guid.NewGuid(),
            Location = "Building A",
            Identifier = "Room 1",
            Capacity = 40
        };
    }
}
