using AiDev.Features.Agent;

namespace AiDev.Executors;

/// <summary>
/// All inputs an executor needs to run an agent session.
/// Replaces the previous flat parameter list on IAgentExecutor.RunAsync.
/// </summary>
/// <param name="CancellationToken">Honour this to support StopAgent.</param>
/// <param name="ReportPid">Optional callback invoked once the executor has an OS-level PID to report. HTTP-based executors that have no PID may ignore this.</param>
/// <param name="ReportWarning">Optional callback invoked when the executor detects a warning condition (e.g. stall). The runner wires this to update agent status so warnings are visible in the UI.</param>
/// <param name="Secrets">Project secrets to inject as environment variables. Values are sensitive — never log them.</param>
/// <param name="ThinkingLevel">Extended reasoning budget. Off = no thinking. Only applied when the model has <see cref="ModelCapabilities.Reasoning"/> and the executor supports it.</param>
/// <param name="EnabledSkills">Skill keys enabled for this agent (e.g. ["git-read", "git-write"]). Empty means the executor uses its own defaults.</param>
/// <param name="Trigger">Optional metadata describing what triggered this agent session.</param>
/// <param name="Prompt">The task prompt to inject.</param>
/// <param name="ModelId">Fully-resolved model identifier (e.g. "claude-sonnet-4-6", "llama3.2").</param>
/// <param name="WorkingDir">The agent's directory (contains CLAUDE.md, inbox, etc.).</param>
/// <param name="ProjectSlug">The slug of the project associated with the current agent session.</param>
/// <param name="WorkspaceRoot">The absolute path to the shared workspace root.</param>
public sealed record ExecutorContext(
    RootDir WorkspaceRoot,
    ProjectSlug ProjectSlug,
    AgentDir WorkingDir,
    string ModelId,
    string Prompt,
    IReadOnlyList<string> EnabledSkills,
    Action<int>? ReportPid,
    AgentLaunchTrigger? Trigger = null,
    ThinkingLevel ThinkingLevel = default,
    IReadOnlyDictionary<string, string>? Secrets = null,
    Action<string>? ReportWarning = null,
    CancellationToken CancellationToken = default);
