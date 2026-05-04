namespace Chronos.Agent.Configuration;

/// <summary>Configuration for the university Ollama API.</summary>
public class OllamaOptions
{
    public const string SectionName = "Agent:Ollama";

    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = "llama4";
}

/// <summary>Configuration for the Puter free AI API.</summary>
public class PuterOptions
{
    public const string SectionName = "Agent:Puter";

    public string BaseUrl { get; set; } = "https://api.puter.com";
    public string ApiToken { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
}
