using System.Text.Json;
using System.Text.Json.Serialization;
using Chronos.Agent.Conversation;
using Chronos.Agent.Domain;

namespace Chronos.Agent.Extraction;

/// <summary>
/// Uses the LLM extraction prompt to parse conversation history into a structured ConstraintDraft.
/// Validates extracted keys against KnownConstraintKeys.
/// </summary>
public class ConstraintExtractor
{
    private readonly ILlmAdapter _llmAdapter;

    public ConstraintExtractor(ILlmAdapter llmAdapter)
    {
        _llmAdapter = llmAdapter ?? throw new ArgumentNullException(nameof(llmAdapter));
    }

    public async Task<ConstraintDraft> ExtractAsync(
        IReadOnlyList<ChatMessage> conversationMessages,
        CancellationToken cancellationToken = default)
    {
        var extractionMessages = new List<ChatMessage>
        {
            new("system", PromptTemplates.ExtractionSystemPrompt)
        };
        extractionMessages.AddRange(conversationMessages);

        var response = await _llmAdapter.ChatAsync(
            extractionMessages,
            new LlmOptions { JsonMode = true },
            cancellationToken);

        return ParseResponse(response.Content);
    }

    private static ConstraintDraft ParseResponse(string json)
    {
        ExtractionResult? result;
        try
        {
            result = JsonSerializer.Deserialize<ExtractionResult>(json);
        }
        catch (JsonException ex)
        {
            throw new ExtractionException($"Failed to parse LLM extraction response as JSON: {ex.Message}", ex);
        }

        if (result is null)
            throw new ExtractionException("LLM extraction returned null.");

        var draft = new ConstraintDraft();

        foreach (var c in result.HardConstraints ?? [])
        {
            if (KnownConstraintKeys.IsValid(c.Key))
                draft.AddHardConstraint(c.Key, c.Value);
        }

        foreach (var p in result.SoftPreferences ?? [])
        {
            if (KnownConstraintKeys.IsValid(p.Key))
                draft.AddSoftPreference(p.Key, p.Value);
        }

        return draft;
    }

    private sealed class ExtractionResult
    {
        [JsonPropertyName("hardConstraints")]
        public List<KeyValueItem>? HardConstraints { get; set; }

        [JsonPropertyName("softPreferences")]
        public List<KeyValueItem>? SoftPreferences { get; set; }
    }

    private sealed class KeyValueItem
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }
}

/// <summary>Thrown when constraint extraction from LLM output fails.</summary>
public class ExtractionException : Exception
{
    public ExtractionException(string message) : base(message) { }
    public ExtractionException(string message, Exception inner) : base(message, inner) { }
}
