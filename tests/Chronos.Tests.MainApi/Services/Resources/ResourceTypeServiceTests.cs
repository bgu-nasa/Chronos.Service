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
public class ResourceTypeServiceTests
{
    private IResourceTypeRepository _resourceTypeRepository;
    private ResourceValidationService _validationService;
    private ResourceTypeService _sut;

    private readonly Guid _orgId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _resourceTypeRepository = Substitute.For<IResourceTypeRepository>();

        _validationService = new ResourceValidationService(
            Substitute.For<IManagementExternalService>(),
            Substitute.For<ISubjectRepository>(),
            Substitute.For<IActivityRepository>(),
            Substitute.For<IResourceRepository>(),
            _resourceTypeRepository,
            Substitute.For<IResourceAttributeRepository>(),
            Substitute.For<IResourceAttributeAssignmentRepository>(),
            Substitute.For<ILogger<ResourceValidationService>>());

        _sut = new ResourceTypeService(
            _resourceTypeRepository,
            _validationService,
            Substitute.For<ILogger<ResourceType>>());
    }

    [Test]
    public async Task GivenValidInput_WhenCreateResourceType_ThenAddsAndReturns()
    {
        var result = await _sut.CreateResourceTypeAsync(_orgId, "Classroom");

        Assert.Multiple(() =>
        {
            Assert.That(result.OrganizationId, Is.EqualTo(_orgId));
            Assert.That(result.Type, Is.EqualTo("Classroom"));
        });
        await _resourceTypeRepository.Received(1).AddAsync(Arg.Any<ResourceType>());
    }

    [Test]
    public void GivenResourceTypeInDifferentOrg_WhenGet_ThenThrowsNotFound()
    {
        var id = Guid.NewGuid();
        _resourceTypeRepository.GetByIdAsync(id)
            .Returns(new ResourceType { Id = id, OrganizationId = Guid.NewGuid(), Type = "Lab" });

        Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.GetResourceTypeAsync(_orgId, id));
    }

    [Test]
    public async Task GivenMixedOrgTypes_WhenGetAll_ThenFiltersToOrg()
    {
        var own = new ResourceType { Id = Guid.NewGuid(), OrganizationId = _orgId, Type = "Room" };
        var foreign = new ResourceType { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), Type = "Hall" };
        _resourceTypeRepository.GetAllAsync().Returns(new List<ResourceType> { own, foreign });

        var result = await _sut.GetResourceTypesAsync(_orgId);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Type, Is.EqualTo("Room"));
    }

    [Test]
    public async Task GivenValidUpdate_WhenUpdateResourceType_ThenPersists()
    {
        var id = Guid.NewGuid();
        _resourceTypeRepository.GetByIdAsync(id)
            .Returns(new ResourceType { Id = id, OrganizationId = _orgId, Type = "Old" });

        await _sut.UpdateResourceTypeAsync(_orgId, id, "Updated");

        await _resourceTypeRepository.Received(1).UpdateAsync(Arg.Is<ResourceType>(rt => rt.Type == "Updated"));
    }

    [Test]
    public async Task GivenExistingType_WhenDelete_ThenCallsRepository()
    {
        var id = Guid.NewGuid();
        var rt = new ResourceType { Id = id, OrganizationId = _orgId, Type = "ToDelete" };
        _resourceTypeRepository.GetByIdAsync(id).Returns(rt);

        await _sut.DeleteResourceTypeAsync(_orgId, id);

        await _resourceTypeRepository.Received(1).DeleteAsync(rt);
    }
}
