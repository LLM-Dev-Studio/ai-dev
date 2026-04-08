using AiDev.Features.Agent;

namespace AiDev.Executors;

/// <summary>
/// All inputs an executor needs to run an agent session.
/// Replaces the previous flat parameter list on IAgentExecutor.RunAsync.
/// </summary>
public sealed record ExecutorContext(
    /// <summary>The agent's directory (contains CLAUDE.md, inbox, etc.).</summary>
    string WorkingDir,

    /// <summary>Fully-resolved model identifier (e.g. "claude-sonnet-4-6", "llama3.2").</summary>
    string ModelId,

    /// <summary>The task prompt to inject.</summary>
    string Prompt,

    /// <summary>Cancellation token — honour this to support StopAgent.</summary>
    CancellationToken CancellationToken,

    /// <summary>
    /// Skill keys enabled for this agent (e.g. ["git-read", "git-write"]).
    /// Empty means the executor uses its own defaults.
    /// </summary>
    IReadOnlyList<string> EnabledSkills,

    /// <summary>
    /// Optional callback invoked once the executor has an OS-level PID to report.
    /// HTTP-based executors that have no PID may ignore this.
    /// </summary>
    Action<int>? ReportPid,

    /// <summary>Optional metadata describing what triggered this agent session.</summary>
    AgentLaunchTrigger? Trigger = null);
