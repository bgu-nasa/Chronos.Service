using Chronos.Data.Repositories.Management;
using Chronos.Domain.Management;
using Chronos.MainApi.Management.Services;
using Chronos.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Chronos.Tests.MainApi.Services.Management;

[TestFixture]
[Category("Unit")]
public class OrganizationServiceTests
{
    private IOrganizationRepository _organizationRepository = null!;
    private OrganizationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _organizationRepository = Substitute.For<IOrganizationRepository>();

        var validationService = new ManagementValidationService(
            _organizationRepository,
            Substitute.For<IDepartmentRepository>(),
            Substitute.For<ILogger<ManagementValidationService>>());

        _service = new OrganizationService(
            _organizationRepository,
            validationService,
            Substitute.For<ILogger<OrganizationService>>());
    }

    [Test]
    public async Task GivenName_WhenCreateOrganization_ThenReturnsNewId()
    {
        var id = await _service.CreateOrganizationAsync("Acme Corp");

        Assert.That(id, Is.Not.EqualTo(Guid.Empty));
        await _organizationRepository.Received(1).AddAsync(
            Arg.Is<Organization>(o => o.Name == "Acme Corp" && !o.Deleted));
    }

    [Test]
    public void GivenNullOrg_WhenSetForDeletion_ThenThrowsNotFound()
    {
        _organizationRepository.GetByIdAsync(Arg.Any<Guid>()).ReturnsNull();

        Assert.ThrowsAsync<NotFoundException>(() =>
            _service.SetForDeletionAsync(Guid.NewGuid()));
    }

    [Test]
    public void GivenAlreadyDeletedOrg_WhenSetForDeletion_ThenThrowsBadRequest()
    {
        var orgId = Guid.NewGuid();
        _organizationRepository.GetByIdAsync(orgId)
            .Returns(new Organization { Id = orgId, Deleted = true });

        Assert.ThrowsAsync<BadRequestException>(() =>
            _service.SetForDeletionAsync(orgId));
    }

    [Test]
    public async Task GivenActiveOrg_WhenSetForDeletion_ThenMarksDeletedWithTimestamp()
    {
        var orgId = Guid.NewGuid();
        var org = new Organization { Id = orgId, Deleted = false };
        _organizationRepository.GetByIdAsync(orgId).Returns(org);

        await _service.SetForDeletionAsync(orgId);

        Assert.That(org.Deleted, Is.True);
        Assert.That(org.DeletedTime, Is.Not.EqualTo(default(DateTime)));
        await _organizationRepository.Received(1).UpdateAsync(org);
    }

    [Test]
    public void GivenNullOrg_WhenRestore_ThenThrowsNotFound()
    {
        _organizationRepository.GetByIdAsync(Arg.Any<Guid>()).ReturnsNull();

        Assert.ThrowsAsync<NotFoundException>(() =>
            _service.RestoreDeletedOrganizationAsync(Guid.NewGuid()));
    }

    [Test]
    public void GivenActiveOrg_WhenRestore_ThenThrowsBadRequest()
    {
        var orgId = Guid.NewGuid();
        _organizationRepository.GetByIdAsync(orgId)
            .Returns(new Organization { Id = orgId, Deleted = false });

        Assert.ThrowsAsync<BadRequestException>(() =>
            _service.RestoreDeletedOrganizationAsync(orgId));
    }

    [Test]
    public async Task GivenDeletedOrg_WhenRestore_ThenClearsDeletedFlag()
    {
        var orgId = Guid.NewGuid();
        var org = new Organization { Id = orgId, Deleted = true, DeletedTime = DateTime.UtcNow };
        _organizationRepository.GetByIdAsync(orgId).Returns(org);

        await _service.RestoreDeletedOrganizationAsync(orgId);

        Assert.That(org.Deleted, Is.False);
        Assert.That(org.DeletedTime, Is.EqualTo(default(DateTime)));
    }
}
