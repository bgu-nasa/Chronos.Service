using Chronos.Data.Context;
using Chronos.Data.Repositories.Auth;
using Chronos.Data.Repositories.Management;
using Chronos.Data.Repositories.Resources;
using Chronos.Data.Repositories.Schedule;
using Chronos.Offboarding.Removers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// Required by AppDbContext — null in non-web context, bypasses tenant query filters
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
}

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Auth repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Management repositories
builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddScoped<IDepartmentRepository, DepartmentRepository>();
builder.Services.AddScoped<IRoleAssignmentRepository, RoleAssignmentRepository>();

// Resource repositories
builder.Services.AddScoped<IActivityRepository, ActivityRepository>();
builder.Services.AddScoped<ISubjectRepository, SubjectRepository>();
builder.Services.AddScoped<IResourceRepository, ResourceRepository>();
builder.Services.AddScoped<IResourceTypeRepository, ResourceTypeRepository>();
builder.Services.AddScoped<IResourceAttributeRepository, ResourceAttributeRepository>();
builder.Services.AddScoped<IResourceAttributeAssignmentRepository, ResourceAttributeAssignmentRepository>();

// Schedule repositories
builder.Services.AddScoped<IActivityConstraintRepository, ActivityConstraintRepository>();
builder.Services.AddScoped<IAssignmentRepository, AssignmentRepository>();
builder.Services.AddScoped<ISchedulingPeriodRepository, SchedulingPeriodRepository>();
builder.Services.AddScoped<ISlotRepository, SlotRepository>();
builder.Services.AddScoped<IUserConstraintRepository, UserConstraintRepository>();
builder.Services.AddScoped<IUserPreferenceRepository, UserPreferenceRepository>();
builder.Services.AddScoped<IOrganizationPolicyRepository, OrganizationPolicyRepository>();

// Removers
builder.Services.AddScoped<DepartmentRemover>();
builder.Services.AddScoped<OrganizationRemover>();

var host = builder.Build();
host.Run();