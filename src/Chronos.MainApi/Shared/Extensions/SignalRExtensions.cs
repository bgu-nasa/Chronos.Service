using Chronos.MainApi.Schedule.Messaging;

namespace Chronos.MainApi.Shared.Extensions;

public static class SignalRExtensions
{
    public static IServiceCollection AddSignalRHubs(this IServiceCollection services)
    {
        services.AddSignalR();
        return services;
    }

    public static IEndpointRouteBuilder MapSignalRHubs(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<SchedulingNotificationsHub>("/hubs/scheduling");
        return endpoints;
    }
}
