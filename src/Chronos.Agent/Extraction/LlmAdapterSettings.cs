namespace Chronos.Agent.Extraction;

public sealed class LlmAdapterSettings
{
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string ChatCompletionsPath { get; set; } = "/chat/completions";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
    public int TimeoutSeconds { get; set; } = 20;
    public double Temperature { get; set; } = 0.0;
    public int MaxTokens { get; set; } = 350;
}
