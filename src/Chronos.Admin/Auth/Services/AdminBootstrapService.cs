using Chronos.Admin.Auth.Validation;
using Chronos.Admin.Configuration;
using Chronos.Admin.CredStore.Entities;
using Chronos.Admin.CredStore.Repositories;
using Microsoft.Extensions.Options;
using BCryptNet = BCrypt.Net.BCrypt;

namespace Chronos.Admin.Auth.Services;

public class AdminBootstrapService(
    IAdminUserRepository userRepository,
    IOptions<AdminConfiguration> config) : IAdminBootstrapService
{
    public async Task EnsureBootstrapAsync(CancellationToken cancellationToken = default)
    {
        if (await userRepository.CountAsync(cancellationToken) > 0)
        {
            return;
        }

        var settings = config.Value;

        if (string.IsNullOrWhiteSpace(settings.DefaultEmail)
            || string.IsNullOrWhiteSpace(settings.DefaultPassword))
        {
            throw new InvalidOperationException(
                "No platform admin accounts exist. Set AdminConfiguration__DefaultEmail and "
                + "AdminConfiguration__DefaultPassword (and AdminConfiguration__SecretKey) then run login.");
        }

        if (string.IsNullOrWhiteSpace(settings.SecretKey) || settings.SecretKey.Length < 32)
        {
            throw new InvalidOperationException(
                "AdminConfiguration__SecretKey must be set (minimum 32 characters) before bootstrap.");
        }

        EmailValidator.ValidateEmail(settings.DefaultEmail);
        PasswordValidator.ValidatePassword(settings.DefaultPassword);

        var firstName = string.IsNullOrWhiteSpace(settings.DefaultFirstName)
            ? "Platform"
            : settings.DefaultFirstName;
        var lastName = string.IsNullOrWhiteSpace(settings.DefaultLastName)
            ? "Administrator"
            : settings.DefaultLastName;

        var now = DateTime.UtcNow;
        var user = new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = settings.DefaultEmail.Trim(),
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = BCryptNet.HashPassword(settings.DefaultPassword),
            Verified = true,
            IsBootstrap = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        await userRepository.AddAsync(user, cancellationToken);
    }
}
