using System.Security.Claims;

namespace Chronos.Admin.Auth.Session;

public interface IAdminSessionGuard
{
    Task<ClaimsPrincipal> RequireValidSessionAsync(CancellationToken cancellationToken = default);
}
