using Chronos.Data.Repositories.Auth;
using Chronos.Data.Repositories.Management;
using Chronos.Data.Repositories.Resources;
using Chronos.Data.Repositories.Schedule;
using Microsoft.Extensions.DependencyInjection;

namespace Chronos.Data;

public static class DataDependencyInjectionExtension
{
    public static void AddServiceRepositories(this IServiceCollection services)
    {
        // Auth
        services.AddScoped<IUserRepository, UserRepository>();

        // Management
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IRoleAssignmentRepository, RoleAssignmentRepository>();
        
        // Resources
        services.AddScoped<ISubjectRepository, SubjectRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IResourceRepository, ResourceRepository>();
        services.AddScoped<IResourceTypeRepository, ResourceTypeRepository>();
        services.AddScoped<IResourceAttributeRepository, ResourceAttributeRepository>();
        services.AddScoped<IResourceAttributeAssignmentRepository, ResourceAttributeAssignmentRepository>();

        // Schedule
        services.AddScoped<ISchedulingPeriodRepository, SchedulingPeriodRepository>();
        services.AddScoped<ISlotRepository, SlotRepository>();
        services.AddScoped<IAssignmentRepository, AssignmentRepository>();
        services.AddScoped<IActivityConstraintRepository, ActivityConstraintRepository>();
        services.AddScoped<IOrganizationPolicyRepository, OrganizationPolicyRepository>();
        services.AddScoped<IUserPreferenceRepository, UserPreferenceRepository>();
        services.AddScoped<IUserConstraintRepository, UserConstraintRepository>();
        services.AddScoped<IAppealRepository, AppealRepository>();
    }
}