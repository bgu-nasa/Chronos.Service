namespace Chronos.Admin.Auth.Services;

public interface IAdminBootstrapService
{
    Task EnsureBootstrapAsync(CancellationToken cancellationToken = default);
}
