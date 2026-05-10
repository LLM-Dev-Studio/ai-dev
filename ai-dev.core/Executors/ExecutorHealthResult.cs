namespace AiDev.Executors;

/// <summary>
/// The result of an executor health check.
/// </summary>
/// <param name="CheckedAt">The UTC timestamp when this health check completed.</param>
/// <param name="Duration">The elapsed duration for the health probe.</param>
/// <param name="IsHealthy">Whether the executor is healthy and can run agent sessions.</param>
/// <param name="Message">Human-readable status message (e.g. "claude 1.2.3" or "Connection refused").</param>
/// <param name="Models">Models discovered during the health check (e.g. Ollama's installed models, GitHub Models catalog). Null when the executor uses a static known-models list and performs no runtime discovery (e.g. Claude CLI, Anthropic API).</param>
public sealed record ExecutorHealthResult(
    bool IsHealthy,
    string Message,
    IReadOnlyList<ModelDescriptor>? Models = null,
    DateTimeOffset? CheckedAt = null,
    TimeSpan? Duration = null);
