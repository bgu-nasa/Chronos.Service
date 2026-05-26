using Chronos.Admin.Auth.Contracts;
using Chronos.Admin.Auth.Extensions;
using Chronos.Admin.Auth.Validation;
using Chronos.Admin.CredStore.Entities;
using Chronos.Admin.CredStore.Repositories;
using Chronos.Shared.Exceptions;
using BCryptNet = BCrypt.Net.BCrypt;

namespace Chronos.Admin.Auth.Services;

public class AdminAuthService(
    IAdminUserRepository userRepository,
    IAdminTokenGenerator tokenGenerator) : IAdminAuthService
{
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        EmailValidator.ValidateEmail(request.Email);

        var user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (user is null || !BCryptNet.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("Invalid credentials");
        }

        var token = await tokenGenerator.GenerateTokenAsync(user);
        return new AuthResponse(token);
    }

    public async Task<UserResponse> AddAccountAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        EmailValidator.ValidateEmail(request.Email);

        if (await userRepository.EmailExistsAsync(request.Email, cancellationToken))
        {
            throw new BadRequestException("User with this email already exists");
        }

        PasswordValidator.ValidatePassword(request.Password);

        var now = DateTime.UtcNow;
        var user = new AdminUser
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PasswordHash = BCryptNet.HashPassword(request.Password),
            Verified = false,
            IsBootstrap = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        await userRepository.AddAsync(user, cancellationToken);
        return user.ToUserResponse();
    }

    public async Task<IReadOnlyList<UserResponse>> ListAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await userRepository.GetAllAsync(cancellationToken);
        return users.Select(u => u.ToUserResponse()).ToList();
    }
}
