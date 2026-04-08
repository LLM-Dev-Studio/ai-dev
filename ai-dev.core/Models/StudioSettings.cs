namespace AiDev.Models;

public class StudioSettings
{
    public Dictionary<string, string> Models { get; set; } = new();

    /// <summary>Base URL for the Ollama HTTP API. Defaults to http://localhost:11434.</summary>
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>Anthropic API key for the direct-API executor. Set to null to disable.</summary>
    public string? AnthropicApiKey { get; set; }
}
