using System.Net.Http.Headers;
using Chronos.Domain.Management.Roles;
using Chronos.MainApi.Auth.Contracts;
using Chronos.MainApi.Management.Contracts;
using Chronos.Tests.Acceptance.Infrastructure;

namespace Chronos.Tests.Acceptance.Support;

/// <summary>
/// One-stop fixture for acceptance tests. Boots a fresh API instance, registers a
/// new organization with an administrator, and exposes an authenticated, org-scoped
/// <see cref="AdminClient"/> — collapsing the register → token → x-org-id setup into
/// a single call. Use <see cref="Seed"/> to create domain data through the API.
/// </summary>
public sealed class AcceptanceContext : IDisposable
{
    public ChronosApiFactory Factory { get; }
    public HttpClient AdminClient { get; }
    public Guid OrganizationId { get; }
    public Guid AdminUserId { get; }
    public string AdminEmail { get; }

    /// <summary>Seeds domain data through the API as the organization administrator.</summary>
    public Seeder Seed { get; }

    private AcceptanceContext(
        ChronosApiFactory factory,
        HttpClient adminClient,
        Guid organizationId,
        Guid adminUserId,
        string adminEmail)
    {
        Factory = factory;
        AdminClient = adminClient;
        OrganizationId = organizationId;
        AdminUserId = adminUserId;
        AdminEmail = adminEmail;
        Seed = new Seeder(adminClient);
    }

    /// <summary>
    /// Boots the API, registers a new organization + administrator, and returns a
    /// context whose <see cref="AdminClient"/> is authenticated and scoped to it.
    /// </summary>
    public static async Task<AcceptanceContext> CreateAsync(string? organizationName = null)
    {
        var factory = new ChronosApiFactory();
        var client = factory.CreateClient();

        var email = $"admin-{Guid.NewGuid():N}@chronos.test";
        var register = await client.PostJsonAsync("/api/auth/register", new RegisterRequest(
            AdminUser: new CreateUserRequest(email, "Acceptance", "Admin", TestConstants.DefaultPassword),
            OrganizationName: organizationName ?? $"Acceptance Org {Guid.NewGuid():N}",
            Plan: "free",
            InviteCode: TestConstants.InviteCode));
        register.EnsureSuccessStatusCode();

        var auth = await register.ReadJsonAsync<AuthResponse>()
                   ?? throw new InvalidOperationException("Registration returned no auth token.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var info = await (await client.GetAsync("/api/management/organization/info"))
                       .ReadJsonAsync<OrganizationInformation>()
                   ?? throw new InvalidOperationException("Could not read organization info after registration.");
        client.SetOrgHeader(info.Id);

        var adminUserId = info.UserRoles.FirstOrDefault()?.UserId ?? Guid.Empty;

        return new AcceptanceContext(factory, client, info.Id, adminUserId, email);
    }

    /// <summary>
    /// Creates an additional client authenticated as a (synthetic) user holding
    /// <paramref name="role"/> within this organization. Intended for authorization
    /// scenarios where a specific role's access needs to be exercised.
    /// </summary>
    public HttpClient CreateClientAs(Role role, Guid? userId = null, string? email = null)
    {
        var id = userId ?? Guid.NewGuid();
        return Factory.CreateAuthenticatedClient(
            id,
            email ?? $"{role}-{id:N}@chronos.test",
            OrganizationId,
            new SimpleRoleForToken(role, OrganizationId));
    }

    public void Dispose()
    {
        AdminClient.Dispose();
        Factory.Dispose();
    }
}
