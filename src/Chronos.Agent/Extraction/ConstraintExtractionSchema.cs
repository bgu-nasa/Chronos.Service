using System.Text.Json;

namespace Chronos.Agent.Extraction;

/// <summary>
/// Builds the JSON Schema passed to Ollama's <c>format</c> field for structured outputs.
/// Ollama (>= 0.5.0) constrains generation to match the schema, so the model can no longer
/// invent constraint keys outside the known catalog. Value formats are still validated
/// post-extraction by <see cref="ConstraintValueValidator"/>, since Ollama's schema support
/// for string patterns/enums on values is inconsistent across models.
/// </summary>
public static class ConstraintExtractionSchema
{
    /// <summary>
    /// Returns the JSON Schema (as a <see cref="JsonElement"/>) describing the expected
    /// extraction response shape with <c>key</c> values restricted to the known catalog.
    /// </summary>
    public static JsonElement Build()
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                hardConstraints = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            key = new
                            {
                                type = "string",
                                @enum = KnownConstraintKeys.HardConstraintKeys.ToArray()
                            },
                            value = new { type = "string" },
                            // Optional ISO week number (1..53) for one-time constraints.
                            // Null/omitted = recurring across the whole scheduling period.
                            weekNum = new { type = new[] { "integer", "null" } }
                        },
                        required = new[] { "key", "value" },
                        additionalProperties = false
                    }
                },
                softPreferences = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            key = new
                            {
                                type = "string",
                                @enum = KnownConstraintKeys.SoftPreferenceKeys.ToArray()
                            },
                            value = new { type = "string" }
                        },
                        required = new[] { "key", "value" },
                        additionalProperties = false
                    }
                }
            },
            required = new[] { "hardConstraints", "softPreferences" },
            additionalProperties = false
        };

        var json = JsonSerializer.Serialize(schema);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
