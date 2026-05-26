using Chronos.Shared.Exceptions;

namespace Chronos.Admin.Auth.Validation;

public static class PasswordValidator
{
    private const int MinLength = 8;
    private const int MaxLength = 128;

    public static void ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new BadRequestException("Password cannot be empty");
        }

        if (password.Length < MinLength)
        {
            throw new BadRequestException($"Password must be at least {MinLength} characters long");
        }

        if (password.Length > MaxLength)
        {
            throw new BadRequestException($"Password must not exceed {MaxLength} characters");
        }

        if (password.Any(char.IsWhiteSpace))
        {
            throw new BadRequestException("Password cannot contain whitespace characters");
        }

        if (!password.Any(char.IsUpper))
        {
            throw new BadRequestException("Password must contain at least one uppercase letter (A-Z)");
        }

        if (!password.Any(char.IsLower))
        {
            throw new BadRequestException("Password must contain at least one lowercase letter (a-z)");
        }

        if (!password.Any(char.IsDigit))
        {
            throw new BadRequestException("Password must contain at least one digit (0-9)");
        }
    }
}
