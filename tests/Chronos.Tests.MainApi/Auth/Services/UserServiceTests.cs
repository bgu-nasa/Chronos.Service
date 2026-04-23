using Chronos.Data.Repositories.Auth;
using Chronos.Domain.Auth;
using Chronos.MainApi.Auth.Contracts;
using Chronos.MainApi.Auth.Services;
using Chronos.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Chronos.Tests.MainApi.Auth.Services;

[TestFixture]
[Category("Unit")]
public class UserServiceTests
{
    private ILogger<UserService> _logger;
    private IUserRepository _userRepository;
    private UserService _sut;

    private readonly Guid _orgId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _logger = Substitute.For<ILogger<UserService>>();
        _userRepository = Substitute.For<IUserRepository>();
        _sut = new UserService(_logger, _userRepository);
    }

    #region GetUserAsync

    [Test]
    public void GivenUserNotFound_WhenGetUser_ThenThrowsNotFound()
    {
        _userRepository.GetByIdAsync(Arg.Any<Guid>()).Returns((User?)null);

        Assert.ThrowsAsync<NotFoundException>(() => _sut.GetUserAsync(_orgId, Guid.NewGuid()));
    }

    [Test]
    public void GivenUserInDifferentOrg_WhenGetUser_ThenThrowsNotFound()
    {
        var user = MakeUser(Guid.NewGuid());
        _userRepository.GetByIdAsync(user.Id).Returns(user);

        Assert.ThrowsAsync<NotFoundException>(() => _sut.GetUserAsync(_orgId, user.Id));
    }

    [Test]
    public async Task GivenUserInSameOrg_WhenGetUser_ThenReturnsResponse()
    {
        var user = MakeUser(_orgId);
        _userRepository.GetByIdAsync(user.Id).Returns(user);

        var result = await _sut.GetUserAsync(_orgId, user.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(user.Id.ToString()));
            Assert.That(result.Email, Is.EqualTo(user.Email));
            Assert.That(result.FirstName, Is.EqualTo(user.FirstName));
        });
    }

    #endregion

    #region GetUsersAsync

    [Test]
    public async Task GivenMixedOrgUsers_WhenGetUsers_ThenReturnsOnlyMatchingOrg()
    {
        var ownUser = MakeUser(_orgId);
        var foreignUser = MakeUser(Guid.NewGuid());
        _userRepository.GetAllAsync().Returns(new List<User> { ownUser, foreignUser });

        var result = (await _sut.GetUsersAsync(_orgId)).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo(ownUser.Id.ToString()));
    }

    #endregion

    #region UpdateUserProfileAsync

    [Test]
    public void GivenUserNotFound_WhenUpdateProfile_ThenThrowsNotFound()
    {
        _userRepository.GetByIdAsync(Arg.Any<Guid>()).Returns((User?)null);
        var request = new UserUpdateRequest("A", "B", null);

        Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateUserProfileAsync(_orgId, Guid.NewGuid(), request));
    }

    [Test]
    public void GivenUserInDifferentOrg_WhenUpdateProfile_ThenThrowsNotFound()
    {
        var user = MakeUser(Guid.NewGuid());
        _userRepository.GetByIdAsync(user.Id).Returns(user);
        var request = new UserUpdateRequest("A", "B", null);

        Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateUserProfileAsync(_orgId, user.Id, request));
    }

    [Test]
    public async Task GivenValidRequest_WhenUpdateProfile_ThenUpdatesFields()
    {
        var user = MakeUser(_orgId);
        _userRepository.GetByIdAsync(user.Id).Returns(user);
        var request = new UserUpdateRequest("Updated", "Name", "https://example.com/avatar.png");

        await _sut.UpdateUserProfileAsync(_orgId, user.Id, request);

        await _userRepository.Received(1).UpdateAsync(Arg.Is<User>(u =>
            u.FirstName == "Updated" &&
            u.LastName == "Name" &&
            u.AvatarUrl == "https://example.com/avatar.png"));
    }

    #endregion

    #region DeleteUserAsync

    [Test]
    public void GivenUserNotFound_WhenDelete_ThenThrowsNotFound()
    {
        _userRepository.GetByIdAsync(Arg.Any<Guid>()).Returns((User?)null);

        Assert.ThrowsAsync<NotFoundException>(() => _sut.DeleteUserAsync(_orgId, Guid.NewGuid()));
    }

    [Test]
    public void GivenUserInDifferentOrg_WhenDelete_ThenThrowsNotFound()
    {
        var user = MakeUser(Guid.NewGuid());
        _userRepository.GetByIdAsync(user.Id).Returns(user);

        Assert.ThrowsAsync<NotFoundException>(() => _sut.DeleteUserAsync(_orgId, user.Id));
    }

    [Test]
    public async Task GivenUserInSameOrg_WhenDelete_ThenCallsRepository()
    {
        var user = MakeUser(_orgId);
        _userRepository.GetByIdAsync(user.Id).Returns(user);

        await _sut.DeleteUserAsync(_orgId, user.Id);

        await _userRepository.Received(1).DeleteAsync(user);
    }

    #endregion

    private static User MakeUser(Guid orgId)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "hash",
            OrganizationId = orgId
        };
    }
}
