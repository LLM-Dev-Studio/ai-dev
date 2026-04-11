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
    /// The executor to use for post-session insights generation (e.g. "anthropic", "ollama", "claude").
    /// Leave null or empty to disable insights. Incurs an extra AI call per session.
    /// </summary>
    public string? InsightsExecutor { get; set; }

    /// <summary>
    /// The model identifier to use for insights generation (e.g. "claude-haiku-4-5-20251001").
    /// Each executor has its own set of valid model IDs.
    /// When omitted, InsightsService picks the first known model for the configured executor.
    /// </summary>
    public string? InsightsModel { get; set; }
}
