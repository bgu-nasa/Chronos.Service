using Chronos.MainApi.Schedule.Messaging;
using Chronos.MainApi.Schedule.Services;
using Microsoft.Extensions.Options;

namespace Chronos.MainApi.Schedule;

public static class ModuleDiExtension
{
    public static void AddScheduleModule(this IServiceCollection services, IConfiguration configuration)
    {
        // RabbitMQ Configuration
        services.Configure<RabbitMqOptions>(
            configuration.GetSection(RabbitMqOptions.SectionName)
        );

        // RabbitMQ Infrastructure
        services.AddSingleton<IRabbitMqConnectionFactory, RabbitMqConnectionFactory>();
        services.AddSingleton<IMessagePublisher, MessagePublisher>();

        // Services
        services.AddScoped<ISchedulingPeriodService, SchedulingPeriodService>();
        services.AddScoped<ISlotService, SlotService>();
        services.AddScoped<IAssignmentService, AssignmentService>();

        services.AddScoped<IActivityConstraintService, ActivityConstraintService>();
        services.AddScoped<IOrganizationPolicyService, OrganizationPolicyService>();
        services.AddScoped<IUserPreferenceService, UserPreferenceService>();
        services.AddScoped<IUserConstraintService, UserConstraintService>();

        // External Services
        services.AddScoped<IExternalActivityService, ExternalActivityService>();
        services.AddScoped<IExternalResourceService, ExternalResourceService>();
        services.AddScoped<IExternalSubjectService, ExternalSubjectService>();
    }
}