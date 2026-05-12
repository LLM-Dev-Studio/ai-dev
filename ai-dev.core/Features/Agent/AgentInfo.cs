using AiDev.Executors;

namespace AiDev.Features.Agent;

/// <summary>
/// Represents the shared metadata and derived status for an agent.
/// </summary>
public abstract record AgentInfo
{
    /// <summary>Gets the agent slug.</summary>
    public required AgentSlug             Slug          { get; init; }
    /// <summary>Gets the agent display name.</summary>
    public required string                Name          { get; init; }
    /// <summary>Gets the agent role.</summary>
    public required string                Role          { get; init; }
    /// <summary>Gets the agent description.</summary>
    public required string                Description   { get; init; }
    /// <summary>Gets the configured model identifier.</summary>
    public required string                Model         { get; init; }
    /// <summary>Gets the configured executor.</summary>
    public required AgentExecutorName     Executor      { get; init; }
    /// <summary>Gets the enabled skills.</summary>
    public required IReadOnlyList<string> Skills        { get; init; }
    /// <summary>Gets the configured thinking level.</summary>
    public required ThinkingLevel         ThinkingLevel { get; init; }
    /// <summary>Gets the current inbox count.</summary>
    public required int                   InboxCount    { get; init; }
    /// <summary>Gets the optional failover metadata.</summary>
    public          AgentFailover?        Failover      { get; init; }

    // ── XAML-bindable projections ────────────────────────────────────────────
    // These delegate to the concrete type so existing bindings need no changes.

    /// <summary>
    /// Gets the derived runtime status of the agent.
    /// </summary>
    public AgentStatus Status => this switch
    {
        AgentInfoRunning => AgentStatus.Running,
        AgentInfoFailed  => AgentStatus.Error,
        _                => AgentStatus.Idle,
    };

    /// <summary>
    /// Gets the timestamp of the most recent run when available.
    /// </summary>
    public DateTime? LastRunAt => this switch
    {
        AgentInfoRunning r => r.StartedAt,
        AgentInfoFailed  f => f.PreviousRunAt,
        AgentInfoIdle    i => i.PreviousRunAt,
        _                  => null,
    };

    /// <summary>
    /// Gets the last error message when the agent is failed.
    /// </summary>
    public string?   LastError   => (this as AgentInfoFailed)?.Failure.Error;
    /// <summary>
    /// Gets the timestamp of the last error when the agent is failed.
    /// </summary>
    public DateTime? LastErrorAt => (this as AgentInfoFailed)?.Failure.OccurredAt;
}

/// <summary>Agent is not currently running. PreviousRunAt records the last session start, if any.</summary>
public sealed record AgentInfoIdle : AgentInfo
{
    public DateTime? PreviousRunAt { get; init; }
}

/// <summary>Agent session is active. StartedAt is always present.</summary>
public sealed record AgentInfoRunning : AgentInfo
{
    public required DateTime StartedAt { get; init; }
}

/// <summary>Agent's last session ended in failure. Failure details are always present.</summary>
public sealed record AgentInfoFailed : AgentInfo
{
    public required AgentFailure Failure       { get; init; }
    public          DateTime?    PreviousRunAt { get; init; }
}
