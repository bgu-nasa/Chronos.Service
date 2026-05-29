using System.Text.RegularExpressions;
using Chronos.Shared.Exceptions;

namespace Chronos.Admin.Auth.Validation;

public static class EmailValidator
{
    private const int MaxLength = 254;
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static void ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new BadRequestException("Email cannot be empty");
        }

        if (email.Length > MaxLength)
        {
            throw new BadRequestException($"Email must not exceed {MaxLength} characters");
        }

        if (!EmailRegex.IsMatch(email))
        {
            throw new BadRequestException("Email format is invalid");
        }
    }
}
