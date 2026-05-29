using Microsoft.Extensions.DependencyInjection;

namespace Chronos.Admin.Auth.Session;

/// <summary>
/// CLI session file persistence and JWT validation for platform admin commands.
/// </summary>
public static class AdminSessionModuleExtensions
{
    public static IServiceCollection AddAdminSession(this IServiceCollection services)
    {
        services.AddSingleton<IAdminSessionStore, AdminSessionStore>();
        services.AddSingleton<IAdminTokenValidator, AdminTokenValidator>();
        services.AddSingleton<IAdminSessionGuard, AdminSessionGuard>();

        return services;
    }
}
