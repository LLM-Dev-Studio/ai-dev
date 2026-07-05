namespace AiDev.Executors;

/// <summary>
/// The outcome of a completed executor session.
/// Replaces the raw int exit code previously returned by IAgentExecutor.RunAsync.
/// </summary>
/// <param name="ExitCode">The process exit code, or a custom code defined by the executor implementation to represent specific outcomes (e.g. rate limit detected).</param>
/// <param name="IsRateLimited">True when the executor detected a rate-limit response during the session. When true, AgentRunnerService will suppress re-launches and skip inbox archiving.</param>
/// <param name="PreserveInbox">True when the current inbox items should be preserved so the user can fix the issue and retry.</param>
/// <param name="ErrorMessage">Human-readable error message if the session failed in a known way.</param>
/// <param name="Usage">Token consumption for this session. Null when the executor does not report usage.</param>
/// <param name="RequiresHumanDecision">True when the error cannot be resolved automatically and requires the user to act (e.g. the model's context window is too small). AgentRunnerService will create a Decision item so the user sees an actionable prompt in the Decisions panel.</param>
public sealed record ExecutorResult(
    int ExitCode,
    bool IsRateLimited = false,
    bool PreserveInbox = false,
    string? ErrorMessage = null,
    TokenUsage? Usage = null,
    bool RequiresHumanDecision = false);
