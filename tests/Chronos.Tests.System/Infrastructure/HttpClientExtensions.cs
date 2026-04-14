using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Chronos.Tests.System.Infrastructure;

/// <summary>
/// Extension methods for HttpClient to simplify E2E test code.
/// </summary>
public static class HttpClientExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static void SetOrgHeader(this HttpClient client, Guid organizationId)
    {
        client.DefaultRequestHeaders.Remove("x-org-id");
        client.DefaultRequestHeaders.Add("x-org-id", organizationId.ToString());
    }

    public static async Task<HttpResponseMessage> PostJsonAsync<T>(
        this HttpClient client, string url, T payload)
    {
        return await client.PostAsJsonAsync(url, payload, JsonOptions);
    }

    public static async Task<HttpResponseMessage> PatchJsonAsync<T>(
        this HttpClient client, string url, T payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PatchAsync(url, content);
    }

    public static async Task<TResponse?> ReadJsonAsync<TResponse>(
        this HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions);
    }
}
