using Chronos.MainApi.Resources.Services;

namespace Chronos.MainApi.Resources;

public static class ModuleDiExtension
{
    public static void AddResourcesModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Validation Service
        services.AddScoped<ResourceValidationService>();
        services.AddScoped<IExternalSchedulingPeriodService, ExternalSchedulingPeriodService>();

        // Services
        services.AddScoped<ISubjectService, SubjectService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IResourceService, ResourceService>();
        services.AddScoped<IResourceTypeService, ResourceTypeService>();
        services.AddScoped<IResourceAttributeService, ResourceAttributeService>();
        services.AddScoped<IResourceAttributeAssignmentService, ResourceAttributeAssignmentService>();
    }
}