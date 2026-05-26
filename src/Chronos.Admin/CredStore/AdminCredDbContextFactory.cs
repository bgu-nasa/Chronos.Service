using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Chronos.Admin.CredStore;

public class AdminCredDbContextFactory : IDesignTimeDbContextFactory<AdminCredDbContext>
{
    public AdminCredDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "AppSettings");
        if (!Directory.Exists(basePath))
        {
            basePath = Path.Combine(Directory.GetCurrentDirectory(), "src", "Chronos.Admin", "AppSettings");
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.Exists(basePath) ? Path.GetDirectoryName(basePath)! : Directory.GetCurrentDirectory())
            .AddJsonFile(Path.Combine("AppSettings", "appsettings.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = AdminCredStoreConnection.Resolve(configuration);
        var optionsBuilder = new DbContextOptionsBuilder<AdminCredDbContext>();
        optionsBuilder.UseSqlite(connectionString);
        return new AdminCredDbContext(optionsBuilder.Options);
    }
}
