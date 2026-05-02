using Chronos.Data.Context;
using Chronos.MainApi.Auth.Configuration;
using Chronos.MainApi.Schedule.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Chronos.Tests.System.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory that replaces external dependencies
/// (PostgreSQL, RabbitMQ) with in-memory/mock equivalents.
/// </summary>
public class ChronosApiFactory : WebApplicationFactory<AuthConfiguration>
{
    public const string TestSecretKey = "E2ETestSecretKeyThatIsLongEnoughForHmacSha256Signing!";
    public const string TestIssuer = "ChronosApi";
    public const string TestAudience = "ChronosClient";

    public IMessagePublisher MockMessagePublisher { get; } = Substitute.For<IMessagePublisher>();

    private readonly string _dbName = $"ChronosE2E_{Guid.NewGuid()}";

    public ChronosApiFactory()
    {
        // Program.cs validates the connection string and reads AuthConfiguration
        // before ConfigureWebHost runs, so we provide them via environment variables.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            "Host=localhost;Database=chronos_e2e_placeholder");
        Environment.SetEnvironmentVariable("AuthConfiguration__SecretKey", TestSecretKey);
        Environment.SetEnvironmentVariable("AuthConfiguration__Issuer", TestIssuer);
        Environment.SetEnvironmentVariable("AuthConfiguration__Audience", TestAudience);
        Environment.SetEnvironmentVariable("AuthConfiguration__ExpiryMinutes", "60");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            ReplaceDatabase(services);
            ReplaceMessagePublisher(services);
            ConfigureTestAuth(services);
        });
    }

    private void ReplaceDatabase(IServiceCollection services)
    {
        // Remove ALL EF/DbContext registrations to avoid dual-provider conflicts.
        // AddDbContext registers options, context, and internal services that
        // accumulate extensions (Npgsql + InMemory), so we remove everything.
        var typesToRemove = new HashSet<Type>
        {
            typeof(DbContextOptions<AppDbContext>),
            typeof(DbContextOptions),
            typeof(AppDbContext),
        };

        var descriptorsToRemove = services
            .Where(d => typesToRemove.Contains(d.ServiceType))
            .ToList();
        foreach (var descriptor in descriptorsToRemove)
            services.Remove(descriptor);

        // Register fresh options without any prior provider extensions
        var freshOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(_dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        services.AddSingleton(freshOptions);
        services.AddSingleton<DbContextOptions>(freshOptions);
        services.AddScoped<AppDbContext>();
    }

    private void ReplaceMessagePublisher(IServiceCollection services)
    {
        var descriptor = services.SingleOrDefault(
            d => d.ServiceType == typeof(IMessagePublisher));
        if (descriptor != null)
            services.Remove(descriptor);

        services.AddSingleton(MockMessagePublisher);

        var factoryDescriptor = services.SingleOrDefault(
            d => d.ServiceType == typeof(IRabbitMqConnectionFactory));
        if (factoryDescriptor != null)
            services.Remove(factoryDescriptor);

        services.AddSingleton(Substitute.For<IRabbitMqConnectionFactory>());
    }

    private static void ConfigureTestAuth(IServiceCollection services)
    {
        services.Configure<AuthConfiguration>(opts =>
        {
            opts.SecretKey = TestSecretKey;
            opts.Issuer = TestIssuer;
            opts.Audience = TestAudience;
            opts.ExpiryMinutes = 60;
        });
    }

    /// <summary>
    /// Creates an HttpClient pre-configured with a valid JWT and x-org-id header.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(
        Guid userId,
        string email,
        Guid organizationId,
        params SimpleRoleForToken[] roles)
    {
        var token = TestTokenGenerator.GenerateToken(
            userId, email, organizationId, roles,
            TestSecretKey, TestIssuer, TestAudience);

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new global::System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("x-org-id", organizationId.ToString());

        return client;
    }

    /// <summary>
    /// Returns a scoped AppDbContext for direct DB assertions.
    /// Caller must dispose the scope.
    /// </summary>
    public (IServiceScope Scope, AppDbContext DbContext) GetDbContext()
    {
        var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (scope, db);
    }
}
