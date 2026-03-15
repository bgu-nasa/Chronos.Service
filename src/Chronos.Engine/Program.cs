using Chronos.Data.Context;
using Chronos.Data.Repositories.Auth;
using Chronos.Data.Repositories.Management;
using Chronos.Data.Repositories.Resources;
using Chronos.Data.Repositories.Schedule;
using Chronos.Engine.Configuration;
using Chronos.Engine.Constraints;
using Chronos.Engine.Constraints.Evaluation;
using Chronos.Engine.Constraints.Evaluation.Validators;
using Chronos.Engine.Matching;
using Chronos.Engine.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// Load configuration from AppSettings folder
builder
    .Configuration.SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("AppSettings/appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile(
        $"AppSettings/appsettings.{builder.Environment.EnvironmentName}.json",
        optional: true,
        reloadOnChange: true
    )
    .AddEnvironmentVariables();

// Configuration
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection(RabbitMqOptions.SectionName)
);

// Add IHttpContextAccessor (required by AppDbContext, but will be null in non-web context)
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

// Database configuration - use PostgreSQL connection string from configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Auth Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Management Repositories
builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddScoped<IDepartmentRepository, DepartmentRepository>();

// Resource Repositories
builder.Services.AddScoped<IActivityRepository, ActivityRepository>();
builder.Services.AddScoped<ISubjectRepository, SubjectRepository>();
builder.Services.AddScoped<IResourceRepository, ResourceRepository>();
builder.Services.AddScoped<IResourceTypeRepository, ResourceTypeRepository>();
builder.Services.AddScoped<IResourceAttributeRepository, ResourceAttributeRepository>();

// Schedule Repositories
builder.Services.AddScoped<IActivityConstraintRepository, ActivityConstraintRepository>();
builder.Services.AddScoped<IAssignmentRepository, AssignmentRepository>();
builder.Services.AddScoped<ISchedulingPeriodRepository, SchedulingPeriodRepository>();
builder.Services.AddScoped<ISlotRepository, SlotRepository>();
builder.Services.AddScoped<IUserConstraintRepository, UserConstraintRepository>();
builder.Services.AddScoped<IUserPreferenceRepository, UserPreferenceRepository>();
builder.Services.AddScoped<IOrganizationPolicyRepository, OrganizationPolicyRepository>();

// RabbitMQ Infrastructure
builder.Services.AddSingleton<IRabbitMqConnectionFactory, RabbitMqConnectionFactory>();
builder.Services.AddSingleton<IMessagePublisher, MessagePublisher>();

// Constraint Evaluation System
builder.Services.AddSingleton<IConstraintEvaluator, ConstraintEvaluator>();

// Register all constraint validators as singletons
builder.Services.AddSingleton<IConstraintValidator, PreferredWeekdaysValidator>();
builder.Services.AddSingleton<IConstraintValidator, TimeRangeValidator>();
builder.Services.AddSingleton<IConstraintValidator, RequiredCapacityValidator>();
builder.Services.AddSingleton<IConstraintValidator, LocationPreferenceValidator>();
builder.Services.AddSingleton<IConstraintValidator, ActivityTypeCompatibilityValidator>();
builder.Services.AddSingleton<IConstraintValidator, ForbiddenTimeRangeValidator>();
builder.Services.AddSingleton<IConstraintValidator, PreferredTimeRangeValidator>();

// Legacy Constraint Processing (for backward compatibility)
builder.Services.AddScoped<IConstraintProcessor, ActivityConstraintProcessor>();

// Matching Algorithms
builder.Services.AddScoped<PreferenceWeightedRanker>();
builder.Services.AddScoped<IMatchingStrategy, RankingAlgorithmStrategy>();
builder.Services.AddScoped<IMatchingStrategy, OnlineMatchingStrategy>();
builder.Services.AddScoped<MatchingOrchestrator>();

// Consumers
builder.Services.AddHostedService<BatchSchedulingConsumer>();
builder.Services.AddHostedService<OnlineSchedulingConsumer>();

var host = builder.Build();
host.Run();
