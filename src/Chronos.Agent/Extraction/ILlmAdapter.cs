using System.Text.Json;
using Chronos.Agent.Conversation;

namespace Chronos.Agent.Extraction;

/// <summary>
/// Abstraction over LLM providers. Implementations exist for Ollama (uni API)
/// and Puter (free local development API).
/// </summary>
public interface ILlmAdapter
{
    Task<LlmResponse> ChatAsync(
        IReadOnlyList<ChatMessage> messages,
        LlmOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Response from an LLM chat completion.</summary>
public record LlmResponse(string Content);

/// <summary>Optional parameters for LLM requests.</summary>
public record LlmOptions
{
    /// <summary>Override the default model for this request.</summary>
    public string? Model { get; init; }

    /// <summary>Request JSON-formatted output (best-effort, not all providers support this).</summary>
    public bool JsonMode { get; init; }

    /// <summary>
    /// Optional JSON Schema constraining the model's output. When set on a provider that
    /// supports structured outputs (Ollama >= 0.5.0), generation is restricted to values
    /// matching the schema, preventing hallucinated fields. Providers without schema
    /// support fall back to <see cref="JsonMode"/> if it is also set.
    /// </summary>
    public JsonElement? JsonSchema { get; init; }
}
