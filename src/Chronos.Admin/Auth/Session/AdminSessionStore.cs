using System.Runtime.InteropServices;

namespace Chronos.Admin.Auth.Session;

public class AdminSessionStore : IAdminSessionStore
{
    public string SessionFilePath { get; } = ResolveSessionPath();

    public async Task SaveTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(SessionFilePath)!;
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(SessionFilePath, token.Trim(), cancellationToken);
        TryRestrictPermissions(SessionFilePath);
    }

    public async Task<string?> ReadTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SessionFilePath))
        {
            return null;
        }

        var token = await File.ReadAllTextAsync(SessionFilePath, cancellationToken);
        return string.IsNullOrWhiteSpace(token) ? null : token.Trim();
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(SessionFilePath))
        {
            File.Delete(SessionFilePath);
        }

        return Task.CompletedTask;
    }

    private static string ResolveSessionPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".chronos-admin", "session");
    }

    private static void TryRestrictPermissions(string filePath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // Best effort only.
            }
        }
    }
}
