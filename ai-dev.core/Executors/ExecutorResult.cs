namespace AiDev.Executors;

/// <summary>
/// The outcome of a completed executor session.
/// Replaces the raw int exit code previously returned by IAgentExecutor.RunAsync.
/// </summary>
public sealed record ExecutorResult(
    int ExitCode,

    /// <summary>
    /// True when the executor detected a rate-limit response during the session.
    /// When true, AgentRunnerService will suppress re-launches and skip inbox archiving.
    /// </summary>
    bool IsRateLimited = false,

    /// <summary>Human-readable error message if the session failed in a known way.</summary>
    string? ErrorMessage = null);
