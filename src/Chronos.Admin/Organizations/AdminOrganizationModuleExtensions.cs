using Chronos.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chronos.Admin.Organizations;

public static class AdminOrganizationModuleExtensions
{
    public static IServiceCollection AddAdminOrganizations(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = AdminPostgresConnection.Resolve(configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return services;
        }

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IAdminOrganizationService, AdminOrganizationService>();

        return services;
    }

    public static string GetMissingConnectionMessage() =>
        "PostgreSQL is required for org commands. Set ConnectionStrings__AdminConnection "
        + "(Host=localhost) or ConnectionStrings__DefaultConnection in .local.env.";
}
