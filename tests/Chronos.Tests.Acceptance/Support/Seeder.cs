using Chronos.MainApi.Auth.Contracts;
using Chronos.MainApi.Management.Contracts;
using Chronos.Tests.Acceptance.Infrastructure;

namespace Chronos.Tests.Acceptance.Support;

/// <summary>
/// Creates domain data through the public API and returns the created resources,
/// so feature tests can read top-to-bottom instead of repeating setup plumbing.
/// Feature-specific seed methods live in partial files (e.g. Seeder.Scheduling.cs).
/// </summary>
public sealed partial class Seeder(HttpClient client)
{
    public async Task<DepartmentResponse> CreateDepartmentAsync(string name)
    {
        var response = await client.PostJsonAsync("/api/management/department", new DepartmentRequest(name));
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync<DepartmentResponse>()
               ?? throw new InvalidOperationException("Create department returned no body.");
    }

    public async Task<CreateUserResponse> CreateUserAsync(
        string email, string firstName = "Test", string lastName = "User", string? password = null)
    {
        var response = await client.PostJsonAsync("/api/user",
            new CreateUserRequest(email, firstName, lastName, password ?? TestConstants.DefaultPassword));
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync<CreateUserResponse>()
               ?? throw new InvalidOperationException("Create user returned no body.");
    }
}
