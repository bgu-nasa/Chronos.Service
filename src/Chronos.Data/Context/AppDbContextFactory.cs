using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Chronos.Data.Context;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Read connection string from environment variable or use default for local development
        // Set ConnectionStrings__DefaultConnection environment variable for different environments
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=chronos;Username=chronos;Password=chronos123";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        // Create a mock IHttpContextAccessor for design-time (migrations don't need tenant filtering)
        var httpContextAccessor = new HttpContextAccessor();

        return new AppDbContext(optionsBuilder.Options, httpContextAccessor);
    }
}
