using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Chronos.Domain.Management.Roles;
using Microsoft.IdentityModel.Tokens;

namespace Chronos.Tests.System.Infrastructure;

/// <summary>
/// Role descriptor for token generation, matching the app's SimpleRoleAssignment shape.
/// </summary>
public record SimpleRoleForToken(Role Role, Guid OrganizationId, Guid? DepartmentId = null);

/// <summary>
/// Generates valid JWTs that match the application's signing key,
/// issuer, audience, and claim structure.
/// </summary>
public static class TestTokenGenerator
{
    public static string GenerateToken(
        Guid userId,
        string email,
        Guid organizationId,
        SimpleRoleForToken[] roles,
        string secretKey,
        string issuer,
        string audience,
        int expiryMinutes = 60)
    {
        var key = Encoding.UTF8.GetBytes(secretKey);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256Signature);

        var roleAssignments = roles.Select(r => new
        {
            Role = r.Role,
            OrganizationId = r.OrganizationId,
            DepartmentId = r.DepartmentId,
        });
        var rolesJson = JsonSerializer.Serialize(roleAssignments);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Name, "Test User"),
                new Claim("organization", organizationId.ToString()),
                new Claim("roles", rolesJson),
            ]),
            SigningCredentials = credentials,
            Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }

    /// <summary>
    /// Convenience overload using the standard test constants.
    /// </summary>
    public static string GenerateToken(
        Guid userId,
        string email,
        Guid organizationId,
        params SimpleRoleForToken[] roles)
    {
        return GenerateToken(
            userId, email, organizationId, roles,
            ChronosApiFactory.TestSecretKey,
            ChronosApiFactory.TestIssuer,
            ChronosApiFactory.TestAudience);
    }
}
