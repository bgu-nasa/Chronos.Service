namespace Chronos.Admin.Auth.Session;

public interface IAdminSessionStore
{
    string SessionFilePath { get; }
    Task SaveTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<string?> ReadTokenAsync(CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
