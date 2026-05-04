using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chronos.Agent.Conversation;
using Chronos.Agent.Configuration;

namespace Chronos.Agent.Extraction;

/// <summary>
/// LLM adapter for the university Ollama API.
/// Supports the Ollama chat endpoint with optional JSON mode.
/// </summary>
public class OllamaLlmAdapter : ILlmAdapter
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;

    public OllamaLlmAdapter(HttpClient httpClient, OllamaOptions options)
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

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = model,
            ["messages"] = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
            ["stream"] = false
        };

        if (options?.JsonMode == true)
            requestBody["format"] = "json";

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var url = $"{_options.BaseUrl.TrimEnd('/')}/api/chat";
        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<OllamaChatResponse>(responseJson);

        return new LlmResponse(result?.Message?.Content ?? string.Empty);
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}
