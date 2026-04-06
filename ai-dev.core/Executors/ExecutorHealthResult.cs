namespace AiDev.Executors;

/// <summary>
/// The result of an executor health check.
/// </summary>
public sealed record ExecutorHealthResult(
    bool IsHealthy,

    /// <summary>Human-readable status message (e.g. "claude 1.2.3" or "Connection refused").</summary>
    string Message,

    /// <summary>
    /// Optional extra detail lines (e.g. list of available models for Ollama).
    /// Null when not applicable.
    /// </summary>
    IReadOnlyList<string>? Details = null);
