using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chronos.Agent.Conversation;
using Chronos.Agent.Configuration;

namespace Chronos.Agent.Extraction;

/// <summary>
/// LLM adapter for the Puter free AI API (https://api.puter.com/drivers/call).
/// Uses the puter-chat-completion driver interface for local development.
/// </summary>
public class PuterLlmAdapter : ILlmAdapter
{
    private readonly HttpClient _httpClient;
    private readonly PuterOptions _options;

    public PuterLlmAdapter(HttpClient httpClient, PuterOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<LlmResponse> ChatAsync(
        IReadOnlyList<ChatMessage> messages,
        LlmOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var model = options?.Model ?? _options.Model;

        var requestBody = new PuterDriverRequest
        {
            Interface = "puter-chat-completion",
            Driver = "openai-completion",
            Method = "complete",
            Args = new PuterChatArgs
            {
                Messages = messages.Select(m => new PuterMessage
                {
                    Role = m.Role,
                    Content = m.Content
                }).ToArray(),
                Model = model
            }
        };

        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var url = $"{_options.BaseUrl.TrimEnd('/')}/drivers/call";
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiToken);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<PuterDriverResponse>(responseJson);

        return new LlmResponse(result?.Result?.Message?.Content ?? string.Empty);
    }

    #region Puter-specific DTOs (isolated from public API)

    private sealed class PuterDriverRequest
    {
        [JsonPropertyName("interface")]
        public string Interface { get; set; } = string.Empty;

        [JsonPropertyName("driver")]
        public string Driver { get; set; } = string.Empty;

        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("args")]
        public PuterChatArgs Args { get; set; } = new();
    }

    private sealed class PuterChatArgs
    {
        [JsonPropertyName("messages")]
        public PuterMessage[] Messages { get; set; } = Array.Empty<PuterMessage>();

        [JsonPropertyName("model")]
        public string? Model { get; set; }
    }

    private sealed class PuterMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class PuterDriverResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("result")]
        public PuterResult? Result { get; set; }
    }

    private sealed class PuterResult
    {
        [JsonPropertyName("message")]
        public PuterMessage? Message { get; set; }
    }

    #endregion
}
