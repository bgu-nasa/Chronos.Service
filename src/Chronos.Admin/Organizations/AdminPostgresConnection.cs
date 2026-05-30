using Microsoft.Extensions.Configuration;

namespace Chronos.Admin.Organizations;

internal static class AdminPostgresConnection
{
    /// <summary>
    /// Resolves PostgreSQL for the Admin CLI. Prefers <c>ConnectionStrings:AdminConnection</c>
    /// (host machine). Falls back to <c>DefaultConnection</c>, rewriting <c>Host=postgres</c> to
    /// <c>localhost</c> so the same .local.env works for Docker services and local dotnet run.
    /// </summary>
    public static string? Resolve(IConfiguration configuration)
    {
        var admin = configuration.GetConnectionString("AdminConnection");
        if (!string.IsNullOrWhiteSpace(admin))
        {
            return admin;
        }

        var fallback = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(fallback))
        {
            return null;
        }

        return fallback
            .Replace("Host=postgres", "Host=localhost", StringComparison.OrdinalIgnoreCase)
            .Replace("Server=postgres", "Server=localhost", StringComparison.OrdinalIgnoreCase);
    }
}
