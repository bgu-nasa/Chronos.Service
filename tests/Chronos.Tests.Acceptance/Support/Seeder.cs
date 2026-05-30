using Chronos.MainApi.Management.Contracts;
using Chronos.Tests.Acceptance.Infrastructure;

namespace Chronos.Tests.Acceptance.Support;

/// <summary>
/// Creates domain data through the public API and returns the created resources,
/// so feature tests can read top-to-bottom instead of repeating setup plumbing.
/// Extend this per feature as new journeys are added.
/// </summary>
public sealed class Seeder(HttpClient client)
{
    public async Task<DepartmentResponse> CreateDepartmentAsync(string name)
    {
        var response = await client.PostJsonAsync("/api/management/department", new DepartmentRequest(name));
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync<DepartmentResponse>()
               ?? throw new InvalidOperationException("Create department returned no body.");
    }
}
