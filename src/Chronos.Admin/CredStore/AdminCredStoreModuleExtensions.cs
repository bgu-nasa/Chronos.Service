using Chronos.Admin.Configuration;
using Chronos.Admin.CredStore.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chronos.Admin.CredStore;

public static class AdminCredStoreModuleExtensions
{
    public static IServiceCollection AddAdminCredStore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AdminConfiguration>(
            configuration.GetSection(AdminConfiguration.SectionName));

        var connectionString = AdminCredStoreConnection.Resolve(configuration);
        services.AddDbContext<AdminCredDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IAdminUserRepository, AdminUserRepository>();

        return services;
    }
}
