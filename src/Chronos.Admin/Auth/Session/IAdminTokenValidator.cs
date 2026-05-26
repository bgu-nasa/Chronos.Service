using System.Security.Claims;

namespace Chronos.Admin.Auth.Session;

public interface IAdminTokenValidator
{
    ClaimsPrincipal? ValidateToken(string token);
    bool IsExpired(string token);
}
