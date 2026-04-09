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

    /// <summary>
    /// True when the current inbox items should be preserved so the user can fix the issue and retry.
    /// </summary>
    bool PreserveInbox = false,

    /// <summary>Human-readable error message if the session failed in a known way.</summary>
    string? ErrorMessage = null,

    /// <summary>
    /// Token consumption for this session. Null when the executor does not report usage.
    /// </summary>
    TokenUsage? Usage = null);
