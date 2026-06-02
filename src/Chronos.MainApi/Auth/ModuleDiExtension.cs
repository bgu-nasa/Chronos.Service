using Chronos.MainApi.Auth.Configuration;
using Chronos.MainApi.Auth.Services;

namespace Chronos.MainApi.Auth;

public static class ModuleDiExtension
{
    public static void AddAuthModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Configurations
        services.Configure<AuthConfiguration>(configuration.GetSection(nameof(AuthConfiguration)));

        // Services
        services.AddSingleton<HackyInvitationService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITokenGenerator, TokenGenerator>();
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<IUserService, UserService>();
    }
}