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
public class ResourceAttributeServiceTests
{
    private IResourceAttributeRepository _resourceAttributeRepository;
    private ResourceValidationService _validationService;
    private ResourceAttributeService _sut;

    private readonly Guid _orgId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _resourceAttributeRepository = Substitute.For<IResourceAttributeRepository>();

        _validationService = new ResourceValidationService(
            Substitute.For<IManagementExternalService>(),
            Substitute.For<ISubjectRepository>(),
            Substitute.For<IActivityRepository>(),
            Substitute.For<IResourceRepository>(),
            Substitute.For<IResourceTypeRepository>(),
            _resourceAttributeRepository,
            Substitute.For<IResourceAttributeAssignmentRepository>(),
            Substitute.For<ILogger<ResourceValidationService>>());

        _sut = new ResourceAttributeService(
            _resourceAttributeRepository,
            _validationService,
            Substitute.For<ILogger<ResourceAttributeService>>());
    }

    [Test]
    public async Task GivenValidInput_WhenCreateAttribute_ThenAddsAndReturns()
    {
        var result = await _sut.CreateResourceAttributeAsync(_orgId, "Projector", "Has a projector");

        Assert.Multiple(() =>
        {
            Assert.That(result.OrganizationId, Is.EqualTo(_orgId));
            Assert.That(result.Title, Is.EqualTo("Projector"));
            Assert.That(result.Description, Is.EqualTo("Has a projector"));
        });
        await _resourceAttributeRepository.Received(1).AddAsync(Arg.Any<ResourceAttribute>());
    }

    [Test]
    public void GivenAttributeInDifferentOrg_WhenGet_ThenThrowsNotFound()
    {
        var id = Guid.NewGuid();
        _resourceAttributeRepository.GetByIdAsync(id)
            .Returns(new ResourceAttribute { Id = id, OrganizationId = Guid.NewGuid(), Title = "X" });

        Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.GetResourceAttributeAsync(_orgId, id));
    }

    [Test]
    public async Task GivenMixedOrgAttributes_WhenGetAll_ThenFiltersToOrg()
    {
        var own = new ResourceAttribute { Id = Guid.NewGuid(), OrganizationId = _orgId, Title = "Whiteboard" };
        var foreign = new ResourceAttribute { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), Title = "Screen" };
        _resourceAttributeRepository.GetAllAsync().Returns(new List<ResourceAttribute> { own, foreign });

        var result = await _sut.GetResourceAttributesAsync(_orgId);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Title, Is.EqualTo("Whiteboard"));
    }

    [Test]
    public async Task GivenValidUpdate_WhenUpdateAttribute_ThenPersists()
    {
        var id = Guid.NewGuid();
        _resourceAttributeRepository.GetByIdAsync(id)
            .Returns(new ResourceAttribute { Id = id, OrganizationId = _orgId, Title = "Old", Description = "Old desc" });

        await _sut.UpdateResourceAttributeAsync(_orgId, id, "Updated", "New desc");

        await _resourceAttributeRepository.Received(1).UpdateAsync(
            Arg.Is<ResourceAttribute>(ra => ra.Title == "Updated" && ra.Description == "New desc"));
    }

    [Test]
    public async Task GivenExistingAttribute_WhenDelete_ThenCallsRepository()
    {
        var id = Guid.NewGuid();
        var attr = new ResourceAttribute { Id = id, OrganizationId = _orgId, Title = "ToDelete" };
        _resourceAttributeRepository.GetByIdAsync(id).Returns(attr);

        await _sut.DeleteResourceAttributeAsync(_orgId, id);

        await _resourceAttributeRepository.Received(1).DeleteAsync(attr);
    }
}
