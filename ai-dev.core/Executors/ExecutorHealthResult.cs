namespace AiDev.Executors;

/// <summary>
/// The result of an executor health check.
/// </summary>
public sealed record ExecutorHealthResult(
    bool IsHealthy,

    /// <summary>Human-readable status message (e.g. "claude 1.2.3" or "Connection refused").</summary>
    string Message,

    /// <summary>
    /// Models discovered during the health check (e.g. Ollama's installed models,
    /// GitHub Models catalog). Null when the executor uses a static known-models list
    /// and performs no runtime discovery (e.g. Claude CLI, Anthropic API).
    /// </summary>
    IReadOnlyList<ModelDescriptor>? Models = null);
