using System.Security.Claims;
using Chronos.Shared.Exceptions;

namespace Chronos.Admin.Auth.Session;

public class AdminSessionGuard(
    IAdminSessionStore sessionStore,
    IAdminTokenValidator tokenValidator) : IAdminSessionGuard
{
    public async Task<ClaimsPrincipal> RequireValidSessionAsync(CancellationToken cancellationToken = default)
    {
        var token = await sessionStore.ReadTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new AdminSessionRequiredException("Authentication required. Run login first.");
        }

        if (tokenValidator.IsExpired(token))
        {
            throw new AdminSessionRequiredException("Session expired. Run login again.");
        }

        var principal = tokenValidator.ValidateToken(token);
        if (principal is null)
        {
            throw new AdminSessionRequiredException("Invalid session. Run login again.");
        }

        return principal;
    }
}
