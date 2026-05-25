using Chronos.Admin.Auth.Services;
using Chronos.Admin.Auth.Session;
using Chronos.Admin.Configuration;
using Chronos.Admin.CredStore;
using Chronos.Admin.CredStore.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chronos.Admin.Auth;

public static class AdminAuthModuleExtensions
{
    public static IServiceCollection AddAdminAuthModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AdminConfiguration>(
            configuration.GetSection(AdminConfiguration.SectionName));

        var connectionString = AdminCredStoreConnection.Resolve(configuration);
        services.AddDbContext<AdminCredDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IAdminUserRepository, AdminUserRepository>();
        services.AddScoped<IAdminAuthService, AdminAuthService>();
        services.AddScoped<IAdminTokenGenerator, AdminTokenGenerator>();
        services.AddScoped<IAdminBootstrapService, AdminBootstrapService>();
        services.AddSingleton<IAdminSessionStore, AdminSessionStore>();
        services.AddSingleton<IAdminTokenValidator, AdminTokenValidator>();
        services.AddSingleton<IAdminSessionGuard, AdminSessionGuard>();

        return services;
    }
}
