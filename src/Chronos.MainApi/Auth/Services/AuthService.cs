using Chronos.Data.Repositories.Auth;
using Chronos.Domain.Auth;
using Chronos.MainApi.Auth.Contracts;
using Chronos.MainApi.Auth.Validation;
using Chronos.Shared.Exceptions;
using BCryptNet = BCrypt.Net.BCrypt;

namespace Chronos.MainApi.Auth.Services;

public class AuthService(
    ILogger<AuthService> logger,
    IUserRepository userRepository,
    IOnboardingService onboardingService,
    ITokenGenerator tokenGenerator)
    : IAuthService
{
    private readonly HackyInvitationService _inviteCodeVerificationService = new();
    
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // TODO Remove this after development
        if (!_inviteCodeVerificationService.VerifyInviteCode(request.InviteCode))
        {
            logger.LogWarning("Invalid invite code was sent {inviteCode}", request.InviteCode);
            throw new UnauthorizedException("Chronos service is still under private beta, you may get access by contacting us.");
        }
        
        EmailValidator.ValidateEmail(request.AdminUser.Email);
        
        if (await userRepository.EmailExistsIgnoreFiltersAsync(request.AdminUser.Email))
        {
            throw new BadRequestException("User with this email already exists");
        }

        PasswordValidator.ValidatePassword(request.AdminUser.Password);

        try
        {
            var organizationId = await onboardingService.CreateOrganizationAsync(request.OrganizationName, request.Plan);

            var user = new User
            {
                Id = Guid.NewGuid(),
                FirstName = request.AdminUser.FirstName,
                LastName = request.AdminUser.LastName,
                Email = request.AdminUser.Email,
                PasswordHash = BCryptNet.HashPassword(request.AdminUser.Password),
                OrganizationId = organizationId,
            };

            await userRepository.AddAsync(user);
            await onboardingService.OnboardAdminUserAsync(organizationId, user);

            var token = await tokenGenerator.GenerateTokenAsync(user);

            return new AuthResponse(token);
        }
        catch (Exception ex)
        {
            logger.LogError("Unable to onboard client: {Message}", ex.Message);
            // TODO: Create reverse onboarding process to clean up half-created organizations/users
            // The log below contains PII, that's bad, before alpha fix this with the reverse onboarding
            logger.LogError("User registration that failed: {Email}, manually remove user", request.AdminUser.Email);
            throw new UnexpectedErrorException();
        }
    }

    public async Task<CreateUserResponse> CreateUserAsync(string organizationId, CreateUserRequest request)
    {
        EmailValidator.ValidateEmail(request.Email);
        
        if (await userRepository.GetByEmailAsync(request.Email) is not null)
        {
            throw new BadRequestException("User with this email already exists");
        }

        PasswordValidator.ValidatePassword(request.Password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PasswordHash = BCryptNet.HashPassword(request.Password),
            OrganizationId = Guid.Parse(organizationId)
        };

        await userRepository.AddAsync(user);

        return new CreateUserResponse(user.Id.ToString(), user.Email);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        EmailValidator.ValidateEmail(request.Email);
        
        var user = await userRepository.GetByEmailIgnoreFiltersAsync(request.Email);

        if (user is null || !BCryptNet.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("Invalid credentials");
        }

        var token = await tokenGenerator.GenerateTokenAsync(user);
        return new AuthResponse(token);
    }

    public async Task<AuthResponse> RefreshTokenAsync(Guid userId)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user is null)
        {
            throw new KeyNotFoundException("User not found");
        }

        var token = await tokenGenerator.GenerateTokenAsync(user);
        return new AuthResponse(token);
    }

    public async Task VerifyTokenAsync(Guid userId)
    {
        if (!await userRepository.ExistsAsync(userId))
        {
            throw new UnauthorizedException("Invalid token");
        }
    }

    public async Task UpdatePasswordAsync(Guid userId, UserPasswordUpdateRequest request)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user is null)
        {
            throw new NotFoundException("User not found");
        }

        if (!BCryptNet.Verify(request.OldPassword, user.PasswordHash))
        {
            throw new UnauthorizedException("Invalid current password");
        }

        PasswordValidator.ValidatePassword(request.NewPassword);

        user.PasswordHash = BCryptNet.HashPassword(request.NewPassword);
        await userRepository.UpdateAsync(user);
    }
}
