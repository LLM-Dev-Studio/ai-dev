using AiDev.Features.Agent;

namespace AiDev.Executors;

/// <summary>
/// All inputs an executor needs to run an agent session.
/// Replaces the previous flat parameter list on IAgentExecutor.RunAsync.
/// </summary>
public sealed record ExecutorContext(
    /// <summary>The absolute path to the shared workspace root.</summary>
    string WorkspaceRoot,

    /// <summary>The slug of the project associated with the current agent session.</summary>
    string ProjectSlug,

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
    AgentLaunchTrigger? Trigger = null,

    /// <summary>
    /// Extended reasoning budget. Off = no thinking. Only applied when the model
    /// has <see cref="ModelCapabilities.Reasoning"/> and the executor supports it.
    /// </summary>
    ThinkingLevel ThinkingLevel = ThinkingLevel.Off,

    /// <summary>
    /// Project secrets to inject as environment variables. Values are sensitive — never log them.
    /// </summary>
    IReadOnlyDictionary<string, string>? Secrets = null,

    /// <summary>
    /// Optional callback invoked when the executor detects a warning condition (e.g. stall).
    /// The runner wires this to update agent status so warnings are visible in the UI.
    /// </summary>
    Action<string>? ReportWarning = null);
