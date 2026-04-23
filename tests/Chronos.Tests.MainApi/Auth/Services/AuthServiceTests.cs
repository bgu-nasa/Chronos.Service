using Chronos.Data.Repositories.Auth;
using Chronos.Domain.Auth;
using Chronos.MainApi.Auth.Contracts;
using Chronos.MainApi.Auth.Services;
using Chronos.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Chronos.Tests.MainApi.Auth.Services;

[TestFixture]
[Category("Unit")]
public class AuthServiceTests
{
    private ILogger<AuthService> _logger;
    private IUserRepository _userRepository;
    private IOnboardingService _onboardingService;
    private ITokenGenerator _tokenGenerator;
    private AuthService _sut;

    [SetUp]
    public void SetUp()
    {
        _logger = Substitute.For<ILogger<AuthService>>();
        _userRepository = Substitute.For<IUserRepository>();
        _onboardingService = Substitute.For<IOnboardingService>();
        _tokenGenerator = Substitute.For<ITokenGenerator>();
        _sut = new AuthService(_logger, _userRepository, _onboardingService, _tokenGenerator);
    }

    #region RegisterAsync

    [Test]
    public void GivenInvalidInviteCode_WhenRegister_ThenThrowsUnauthorized()
    {
        var request = MakeRegisterRequest(inviteCode: "invalidcode");

        Assert.ThrowsAsync<UnauthorizedException>(() => _sut.RegisterAsync(request));
    }

    [Test]
    public void GivenDuplicateEmail_WhenRegister_ThenThrowsBadRequest()
    {
        var request = MakeRegisterRequest();
        _userRepository.EmailExistsIgnoreFiltersAsync(request.AdminUser.Email)
            .Returns(true);

        Assert.ThrowsAsync<BadRequestException>(() => _sut.RegisterAsync(request));
    }

    [Test]
    public async Task GivenValidRequest_WhenRegister_ThenCreatesOrgAndUser()
    {
        var orgId = Guid.NewGuid();
        var request = MakeRegisterRequest();

        _userRepository.EmailExistsIgnoreFiltersAsync(request.AdminUser.Email)
            .Returns(false);
        _onboardingService.CreateOrganizationAsync(request.OrganizationName, request.Plan)
            .Returns(orgId);
        _tokenGenerator.GenerateTokenAsync(Arg.Any<User>())
            .Returns("jwt-token");

        var result = await _sut.RegisterAsync(request);

        Assert.That(result.Token, Is.EqualTo("jwt-token"));
        await _userRepository.Received(1).AddAsync(Arg.Is<User>(u =>
            u.Email == request.AdminUser.Email &&
            u.OrganizationId == orgId));
        await _onboardingService.Received(1).OnboardAdminUserAsync(orgId, Arg.Any<User>());
    }

    [Test]
    public void GivenOnboardingFails_WhenRegister_ThenThrowsUnexpectedError()
    {
        var request = MakeRegisterRequest();
        _userRepository.EmailExistsIgnoreFiltersAsync(request.AdminUser.Email)
            .Returns(false);
        _onboardingService.CreateOrganizationAsync(request.OrganizationName, request.Plan)
            .ThrowsAsync(new Exception("DB down"));

        Assert.ThrowsAsync<UnexpectedErrorException>(() => _sut.RegisterAsync(request));
    }

    #endregion

    #region CreateUserAsync

    [Test]
    public void GivenDuplicateEmail_WhenCreateUser_ThenThrowsBadRequest()
    {
        var orgId = Guid.NewGuid().ToString();
        var request = new CreateUserRequest("taken@test.com", "John", "Doe", "Password1");
        _userRepository.GetByEmailAsync("taken@test.com")
            .Returns(new User { Id = Guid.NewGuid(), Email = "taken@test.com", FirstName = "X", LastName = "Y", PasswordHash = "h", OrganizationId = Guid.NewGuid() });

        Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateUserAsync(orgId, request));
    }

    [Test]
    public async Task GivenValidRequest_WhenCreateUser_ThenReturnsNewUserId()
    {
        var orgId = Guid.NewGuid().ToString();
        var request = new CreateUserRequest("new@test.com", "Jane", "Doe", "Password1");
        _userRepository.GetByEmailAsync("new@test.com")
            .Returns((User?)null);

        var result = await _sut.CreateUserAsync(orgId, request);

        Assert.That(result.Email, Is.EqualTo("new@test.com"));
        Assert.That(result.UserId, Is.Not.Null.And.Not.Empty);
        await _userRepository.Received(1).AddAsync(Arg.Is<User>(u =>
            u.Email == "new@test.com" &&
            u.OrganizationId == Guid.Parse(orgId)));
    }

    #endregion

    #region LoginAsync

    [Test]
    public void GivenNonExistentEmail_WhenLogin_ThenThrowsUnauthorized()
    {
        _userRepository.GetByEmailIgnoreFiltersAsync("nobody@test.com")
            .Returns((User?)null);

        var request = new LoginRequest("nobody@test.com", "Password1");
        Assert.ThrowsAsync<UnauthorizedException>(() => _sut.LoginAsync(request));
    }

    [Test]
    public void GivenWrongPassword_WhenLogin_ThenThrowsUnauthorized()
    {
        var user = MakeUser();
        _userRepository.GetByEmailIgnoreFiltersAsync(user.Email)
            .Returns(user);

        var request = new LoginRequest(user.Email, "WrongPassword1");
        Assert.ThrowsAsync<UnauthorizedException>(() => _sut.LoginAsync(request));
    }

    [Test]
    public async Task GivenCorrectCredentials_WhenLogin_ThenReturnsToken()
    {
        var user = MakeUser();
        _userRepository.GetByEmailIgnoreFiltersAsync(user.Email)
            .Returns(user);
        _tokenGenerator.GenerateTokenAsync(user)
            .Returns("login-jwt");

        var request = new LoginRequest(user.Email, "Password1");
        var result = await _sut.LoginAsync(request);

        Assert.That(result.Token, Is.EqualTo("login-jwt"));
    }

    #endregion

    #region RefreshTokenAsync

    [Test]
    public void GivenNonExistentUser_WhenRefreshToken_ThenThrowsKeyNotFound()
    {
        _userRepository.GetByIdAsync(Arg.Any<Guid>())
            .Returns((User?)null);

        Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.RefreshTokenAsync(Guid.NewGuid()));
    }

    [Test]
    public async Task GivenExistingUser_WhenRefreshToken_ThenReturnsNewToken()
    {
        var user = MakeUser();
        _userRepository.GetByIdAsync(user.Id).Returns(user);
        _tokenGenerator.GenerateTokenAsync(user).Returns("refreshed-jwt");

        var result = await _sut.RefreshTokenAsync(user.Id);

        Assert.That(result.Token, Is.EqualTo("refreshed-jwt"));
    }

    #endregion

    #region VerifyTokenAsync

    [Test]
    public void GivenUserDoesNotExist_WhenVerifyToken_ThenThrowsUnauthorized()
    {
        _userRepository.ExistsAsync(Arg.Any<Guid>()).Returns(false);

        Assert.ThrowsAsync<UnauthorizedException>(() => _sut.VerifyTokenAsync(Guid.NewGuid()));
    }

    [Test]
    public void GivenUserExists_WhenVerifyToken_ThenDoesNotThrow()
    {
        var userId = Guid.NewGuid();
        _userRepository.ExistsAsync(userId).Returns(true);

        Assert.DoesNotThrowAsync(() => _sut.VerifyTokenAsync(userId));
    }

    #endregion

    #region UpdatePasswordAsync

    [Test]
    public void GivenUserNotFound_WhenUpdatePassword_ThenThrowsNotFound()
    {
        _userRepository.GetByIdAsync(Arg.Any<Guid>()).Returns((User?)null);

        var request = new UserPasswordUpdateRequest("Old1pass", "New1pass");
        Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdatePasswordAsync(Guid.NewGuid(), request));
    }

    [Test]
    public void GivenWrongOldPassword_WhenUpdatePassword_ThenThrowsUnauthorized()
    {
        var user = MakeUser();
        _userRepository.GetByIdAsync(user.Id).Returns(user);

        var request = new UserPasswordUpdateRequest("WrongOld1", "New1pass");
        Assert.ThrowsAsync<UnauthorizedException>(() => _sut.UpdatePasswordAsync(user.Id, request));
    }

    [Test]
    public async Task GivenCorrectOldPassword_WhenUpdatePassword_ThenUpdatesHash()
    {
        var user = MakeUser();
        _userRepository.GetByIdAsync(user.Id).Returns(user);

        var request = new UserPasswordUpdateRequest("Password1", "NewValid1");
        await _sut.UpdatePasswordAsync(user.Id, request);

        await _userRepository.Received(1).UpdateAsync(Arg.Is<User>(u => u.Id == user.Id));
    }

    #endregion

    #region Helpers

    private static RegisterRequest MakeRegisterRequest(string inviteCode = "0")
    {
        var inviteService = new HackyInvitationService();
        var code = inviteCode == "0" ? inviteService.GenerateInviteCode() : inviteCode;

        return new RegisterRequest(
            new CreateUserRequest("admin@test.com", "Admin", "User", "Password1"),
            "TestOrg",
            "Free",
            code);
    }

    private static User MakeUser()
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1"),
            OrganizationId = Guid.NewGuid()
        };
    }

    #endregion
}
