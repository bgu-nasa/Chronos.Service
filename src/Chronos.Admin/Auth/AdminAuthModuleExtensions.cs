using Chronos.Admin.Auth.Services;
using Chronos.Admin.Auth.Session;
using Chronos.Admin.CredStore;
using Chronos.Admin.Organizations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chronos.Admin.Auth;

public static class AdminAuthModuleExtensions
{
    public static IServiceCollection AddAdminAuthModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAdminCredStore(configuration);

        services.AddScoped<IAdminAuthService, AdminAuthService>();
        services.AddScoped<IAdminTokenGenerator, AdminTokenGenerator>();
        services.AddScoped<IAdminBootstrapService, AdminBootstrapService>();

        services.AddAdminSession();
        services.AddAdminOrganizations(configuration);

        return services;
    }
}
