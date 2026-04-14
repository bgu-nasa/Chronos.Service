using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Chronos.Agent.Conversation;
using Microsoft.Extensions.Options;

namespace Chronos.Agent.Extraction;

public sealed class OpenAiCompatibleLlmAdapter(
    IHttpClientFactory httpClientFactory,
    IOptions<LlmAdapterSettings> settingsOptions) : ILlmAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyDictionary<string, string> CanonicalKeyMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["avoid day"] = "avoid_weekday",
            ["avoid_weekdays"] = "avoid_weekday",
            ["cannot_work_day"] = "avoid_weekday",
            ["cant_work_day"] = "avoid_weekday",
            ["preferred_days"] = "preferred_weekdays",
            ["preferred_day"] = "preferred_weekday",
            ["preferred_morning"] = "preferred_time_morning",
            ["preferred_afternoon"] = "preferred_time_afternoon",
            ["preferred_evening"] = "preferred_time_evening",
            ["preferred_time_range"] = "preferred_timerange"
        };

    private readonly LlmAdapterSettings settings = settingsOptions.Value;

    public async Task<ExtractedConstraintSet> ExtractAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException("Agent LLM API key is not configured.");
        }

        using var request = BuildRequest(messages);
        using var httpClient = httpClientFactory.CreateClient(nameof(OpenAiCompatibleLlmAdapter));
        httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"LLM request failed with {(int)response.StatusCode}: {payload}");
        }

        var completionContent = ExtractAssistantContent(payload);
        return ParseExtractionPayload(completionContent);
    }

    private HttpRequestMessage BuildRequest(IReadOnlyList<ChatMessage> messages)
    {
        var baseUrl = settings.BaseUrl.TrimEnd('/');
        var path = settings.ChatCompletionsPath.Trim();
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        var uri = new Uri($"{baseUrl}{path}", UriKind.Absolute);
        var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        var llmMessages = new List<object>
        {
            new
            {
                role = "system",
                content = "Extract scheduling constraints from the conversation. Return JSON only with this shape: {\"hardConstraints\":[{\"key\":\"...\",\"value\":\"...\"}],\"softPreferences\":[{\"key\":\"...\",\"value\":\"...\"}]}. Use only known keys like avoid_weekday, preferred_weekday, preferred_weekdays, preferred_time_morning, preferred_time_afternoon, preferred_time_evening, preferred_timerange."
            }
        };

        llmMessages.AddRange(messages.Select(m => new
        {
            role = string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user",
            content = m.Content
        }));

        var body = new
        {
            model = settings.Model,
            temperature = settings.Temperature,
            max_tokens = settings.MaxTokens,
            messages = llmMessages
        };

        request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        return request;
    }

    private static string ExtractAssistantContent(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("LLM response did not include choices.");
        }

        var firstChoice = choices[0];
        if (!firstChoice.TryGetProperty("message", out var message) || !message.TryGetProperty("content", out var contentNode))
        {
            throw new InvalidOperationException("LLM response did not include message content.");
        }

        var content = contentNode.GetString();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("LLM response content was empty.");
        }

        return content;
    }

    private static ExtractedConstraintSet ParseExtractionPayload(string content)
    {
        var json = ExtractJsonObject(content);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var set = new ExtractedConstraintSet();
        ParseGroup(root, set.HardConstraints, "hardConstraints", "hard_constraints");
        ParseGroup(root, set.SoftPreferences, "softPreferences", "soft_preferences");

        return set;
    }

    private static void ParseGroup(JsonElement root, List<(string Key, string Value)> target, params string[] propertyNames)
    {
        JsonElement group = default;
        foreach (var propertyName in propertyNames)
        {
            if (root.TryGetProperty(propertyName, out group) && group.ValueKind == JsonValueKind.Array)
            {
                break;
            }

            group = default;
        }

        if (group.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in group.EnumerateArray())
        {
            var key = ReadProperty(item, "key");
            var value = ReadProperty(item, "value");
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var canonicalKey = CanonicalizeKey(key);
            if (ConstraintCatalog.IsKnownHardConstraint(canonicalKey) || ConstraintCatalog.IsKnownSoftPreference(canonicalKey))
            {
                target.Add((canonicalKey, value.Trim()));
            }
        }
    }

    private static string ReadProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var valueNode))
        {
            return string.Empty;
        }

        return valueNode.ValueKind == JsonValueKind.String
            ? valueNode.GetString() ?? string.Empty
            : valueNode.ToString();
    }

    private static string CanonicalizeKey(string rawKey)
    {
        var normalized = rawKey.Trim().Replace('-', '_').Replace(' ', '_').ToLowerInvariant();
        return CanonicalKeyMap.TryGetValue(normalized, out var mapped) ? mapped : normalized;
    }

    private static string ExtractJsonObject(string content)
    {
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            throw new InvalidOperationException("LLM content did not contain a JSON object.");
        }

        return content[start..(end + 1)];
    }
}
