using Chronos.Admin.Configuration;
using Microsoft.Extensions.Configuration;

namespace Chronos.Admin.CredStore;

public static class AdminCredStoreConnection
{
    public static string Resolve(IConfiguration configuration)
    {
        var section = configuration.GetSection(AdminConfiguration.SectionName);
        var fullConnection = section["CredStoreConnection"];
        if (!string.IsNullOrWhiteSpace(fullConnection))
        {
            return fullConnection;
        }

        var path = section["CredStorePath"] ?? "./data/admin-creds.db";
        var absolutePath = Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));

        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return $"Data Source={absolutePath}";
    }

    public static string ResolveCredStorePath(IConfiguration configuration)
    {
        var section = configuration.GetSection(AdminConfiguration.SectionName);
        var fullConnection = section["CredStoreConnection"];
        if (!string.IsNullOrWhiteSpace(fullConnection)
            && fullConnection.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            var dataSource = fullConnection
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(p => p.TrimStart().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase));
            if (dataSource is not null)
            {
                return dataSource["Data Source=".Length..].Trim();
            }
        }

        var path = section["CredStorePath"] ?? "./data/admin-creds.db";
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }
}
