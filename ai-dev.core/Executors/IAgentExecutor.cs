namespace AiDev.Executors;

/// <summary>
/// Abstracts the specific backend used to run an agent session.
/// Implementations live in separate executor projects (ai-dev.executor.claude, ai-dev.executor.ollama, etc.)
/// and register themselves via extension methods, keeping ai-dev.core free of runtime-specific dependencies.
/// </summary>
public interface IAgentExecutor
{
    /// <summary>The executor used when no executor is specified in agent.json.</summary>
    public const string Default = AgentExecutorName.ClaudeValue;

    /// <summary>
    /// Unique identifier for this executor, matched against the "executor" field in agent.json.
    /// </summary>
    string Name { get; }

    /// <summary>Display name shown in the UI (e.g. "Claude CLI", "Ollama").</summary>
    string DisplayName { get; }

    /// <summary>
    /// Skills this executor can grant to agents.
    /// The UI uses this list to render the per-agent skill checklist.
    /// Empty for executors that have no tool-granting concept (e.g. Ollama).
    /// </summary>
    IReadOnlyList<ExecutorSkill> AvailableSkills { get; }

    /// <summary>
    /// Checks whether the runtime is available and ready to run agents.
    /// Called by ExecutorHealthMonitor on a 30-second polling interval.
    /// Should be fast and non-blocking — a simple probe only.
    /// </summary>
    Task<ExecutorHealthResult> CheckHealthAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs an agent session to completion.
    /// All output lines must be written to <paramref name="output"/> as they arrive.
    /// The implementation is responsible for honouring <see cref="ExecutorContext.CancellationToken"/>.
    /// </summary>
    Task<ExecutorResult> RunAsync(ExecutorContext context, ChannelWriter<string> output);
}
