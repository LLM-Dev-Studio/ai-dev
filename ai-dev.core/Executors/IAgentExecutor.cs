namespace AiDev.Executors;

/// <summary>
/// Abstracts the specific backend used to run an agent session.
/// Implementations may launch a child process (Claude CLI), call an HTTP API (Ollama),
/// or use any other mechanism.
/// </summary>
public interface IAgentExecutor
{
    /// <summary>The executor used when no executor is specified in agent.json.</summary>
    public const string Default = "claude";

    /// <summary>
    /// Unique identifier for this executor, e.g. "claude", "ollama".
    /// Matched against the "executor" field in agent.json.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Runs an agent session to completion and returns an exit code (0 = success).
    /// All output lines must be written to <paramref name="output"/> as they arrive.
    /// The implementation is responsible for handling <paramref name="ct"/> cancellation.
    /// </summary>
    /// <param name="workingDir">The agent's directory (contains CLAUDE.md, inbox, etc.).</param>
    /// <param name="modelId">Fully-resolved model identifier (e.g. "claude-sonnet-4-6", "llama3.2").</param>
    /// <param name="prompt">The task prompt.</param>
    /// <param name="output">Channel to write transcript lines to (timestamp prefix added by caller).</param>
    /// <param name="reportPid">
    /// Optional callback invoked once the executor has an OS-level PID to report.
    /// HTTP-based executors that have no PID may ignore this.
    /// </param>
    /// <param name="ct">Cancellation token — honour this to support StopAgent.</param>
    Task<int> RunAsync(string workingDir, string modelId, string prompt,
        ChannelWriter<string> output, Action<int>? reportPid, CancellationToken ct);
}
