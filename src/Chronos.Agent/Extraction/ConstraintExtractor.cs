using System.Text.Json;
using System.Text.Json.Serialization;
using Chronos.Agent.Conversation;
using Chronos.Agent.Domain;

namespace Chronos.Agent.Extraction;

/// <summary>
/// A single piece of extracted output that the agent rejected, with the reason.
/// Surfaced to the user so they know which utterances were dropped instead of silently
/// presenting an incomplete or wrong draft.
/// </summary>
/// <param name="Kind">"hardConstraint" or "softPreference".</param>
/// <param name="Key">The key the LLM produced (may be unknown).</param>
/// <param name="Value">The value the LLM produced.</param>
/// <param name="Reason">Human-readable explanation of why the item was rejected.</param>
public record ExtractionIssue(string Kind, string Key, string Value, string Reason);

/// <summary>
/// Result of an extraction pass: the validated draft plus any items the LLM produced
/// that failed validation. The presence of issues does not abort extraction — valid
/// items are still returned in <see cref="Draft"/>.
/// </summary>
public record ExtractionResult(ConstraintDraft Draft, IReadOnlyList<ExtractionIssue> Issues);

/// <summary>
/// Uses the LLM extraction prompt to parse conversation history into a structured ConstraintDraft.
/// Constrains the LLM output via Ollama's structured-output schema (key restricted to known
/// catalog) and validates value formats post-extraction. Rejected items are surfaced as
/// <see cref="ExtractionIssue"/>s rather than being silently dropped.
/// </summary>
public class ConstraintExtractor
{
    private readonly ILlmAdapter _llmAdapter;
    private static readonly JsonElement Schema = ConstraintExtractionSchema.Build();

    public ConstraintExtractor(ILlmAdapter llmAdapter)
    {
        _llmAdapter = llmAdapter ?? throw new ArgumentNullException(nameof(llmAdapter));
    }

    public async Task<ExtractionResult> ExtractAsync(
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
            new LlmOptions { JsonMode = true, JsonSchema = Schema },
            cancellationToken);

        return ParseResponse(response.Content);
    }

    private static ExtractionResult ParseResponse(string json)
    {
        ExtractionPayload? result;
        try
        {
            result = JsonSerializer.Deserialize<ExtractionPayload>(json);
        }
        catch (JsonException ex)
        {
            throw new ExtractionException($"Failed to parse LLM extraction response as JSON: {ex.Message}", ex);
        }

        if (result is null)
            throw new ExtractionException("LLM extraction returned null.");

        var draft = new ConstraintDraft();
        var issues = new List<ExtractionIssue>();

        foreach (var c in result.HardConstraints ?? [])
            ProcessHardItem(c, draft, issues);

        foreach (var p in result.SoftPreferences ?? [])
            ProcessSoftItem(p, draft, issues);

        return new ExtractionResult(draft, issues);
    }

    private static void ProcessHardItem(
        KeyValueItem item, ConstraintDraft draft, List<ExtractionIssue> issues)
    {
        var key = item.Key ?? string.Empty;
        var value = item.ValueAsString;

        if (!KnownConstraintKeys.HardConstraintKeys.Contains(key))
        {
            issues.Add(new ExtractionIssue("hardConstraint", key, value,
                $"Unknown hardConstraint key '{key}'. Expected one of: " +
                $"{string.Join(", ", KnownConstraintKeys.HardConstraintKeys)}."));
            return;
        }

        var valueError = ConstraintValueValidator.Validate(key, value);
        if (valueError is not null)
        {
            issues.Add(new ExtractionIssue("hardConstraint", key, value, valueError));
            return;
        }

        var weekError = ConstraintValueValidator.ValidateWeekNum(item.WeekNum);
        if (weekError is not null)
        {
            issues.Add(new ExtractionIssue("hardConstraint", key, value, weekError));
            return;
        }

        draft.AddHardConstraint(key, value, item.WeekNum);
    }

    private static void ProcessSoftItem(
        KeyValueItem item, ConstraintDraft draft, List<ExtractionIssue> issues)
    {
        ProcessItem(item, "softPreference", KnownConstraintKeys.SoftPreferenceKeys,
            (k, v) => draft.AddSoftPreference(k, v), issues);
    }

    private static void ProcessItem(
        KeyValueItem item,
        string kind,
        IReadOnlySet<string> validKeys,
        Action<string, string> addToDraft,
        List<ExtractionIssue> issues)
    {
        var key = item.Key ?? string.Empty;
        var value = item.ValueAsString;

        if (!validKeys.Contains(key))
        {
            issues.Add(new ExtractionIssue(kind, key, value,
                $"Unknown {kind} key '{key}'. Expected one of: {string.Join(", ", validKeys)}."));
            return;
        }

        var valueError = ConstraintValueValidator.Validate(key, value);
        if (valueError is not null)
        {
            issues.Add(new ExtractionIssue(kind, key, value, valueError));
            return;
        }

        addToDraft(key, value);
    }

    private sealed class ExtractionPayload
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
        public JsonElement Value { get; set; }

        /// <summary>
        /// Optional ISO week number (1..53) emitted by the LLM for one-time hard
        /// constraints. Null/absent for recurring constraints and for soft preferences.
        /// Maps to <c>UserConstraint.WeekNum</c> on submit.
        /// </summary>
        [JsonPropertyName("weekNum")]
        public int? WeekNum { get; set; }

        public string ValueAsString => Value.ValueKind switch
        {
            JsonValueKind.String => Value.GetString() ?? string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => Value.GetRawText(),
            JsonValueKind.Undefined => string.Empty,
            JsonValueKind.Null => string.Empty,
            _ => Value.GetRawText()
        };
    }
}

/// <summary>Thrown when constraint extraction from LLM output fails.</summary>
public class ExtractionException : Exception
{
    public ExtractionException(string message) : base(message) { }
    public ExtractionException(string message, Exception inner) : base(message, inner) { }
}

