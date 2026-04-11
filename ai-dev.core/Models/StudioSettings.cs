namespace AiDev.Models;

public class StudioSettings
{
    public Dictionary<string, string> Models { get; set; } = new();  // Legacy — kept for migration only; not used for model resolution.

    /// <summary>Base URL for the Ollama HTTP API. Defaults to http://localhost:11434.</summary>
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>Anthropic API key for the direct-API executor. Set to null to disable.</summary>
    public string? AnthropicApiKey { get; set; }

    /// <summary>GitHub personal access token for the GitHub Models executor. Set to null to disable.</summary>
    public string? GitHubToken { get; set; }

    /// <summary>
    /// When true, generates AI-powered session insights via the Anthropic API after each session ends.
    /// Disabled by default because it incurs an extra API call per session.
    /// </summary>
    public bool EnableInsights { get; set; }
}
