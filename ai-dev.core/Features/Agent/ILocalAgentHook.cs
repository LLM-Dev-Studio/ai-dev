using System.Threading.Channels;

namespace AiDev.Features.Agent;

/// <summary>
/// Defines a hook that can intercept or augment local agent execution.
/// </summary>
public interface ILocalAgentHook
{
    /// <summary>
    /// Determines whether the hook applies to the given executor.
    /// </summary>
    /// <param name="executorName">The executor name to evaluate.</param>
    /// <returns><see langword="true"/> when the hook applies; otherwise, <see langword="false"/>.</returns>
    bool IsApplicable(string executorName);

    /// <summary>
    /// Runs the hook for the provided execution context.
    /// </summary>
    /// <param name="context">The local agent hook context.</param>
    /// <param name="output">The output channel for hook messages.</param>
    /// <param name="ct">The cancellation token for the hook execution.</param>
    /// <returns>The hook result.</returns>
    Task<LocalAgentHookResult> RunAsync(
        LocalAgentHookContext context,
        ChannelWriter<string> output,
        CancellationToken ct);
}

/// <summary>
/// Provides context for running a local agent hook.
/// </summary>
/// <param name="Goal">The goal being executed.</param>
/// <param name="WorkingDir">The working directory for the session.</param>
/// <param name="ModelId">The model identifier selected for execution.</param>
/// <param name="ExecutorName">The executor name for the session.</param>
/// <param name="SessionId">The unique session identifier.</param>
public sealed record LocalAgentHookContext(
    string Goal,
    AgentDir WorkingDir,
    string ModelId,
    string ExecutorName,
    Guid SessionId);

/// <summary>
/// Represents the outcome of running a local agent hook.
/// </summary>
/// <param name="Succeeded">Indicates whether the hook completed successfully.</param>
/// <param name="ErrorMessage">The error message when the hook fails.</param>
public sealed record LocalAgentHookResult(
    bool Succeeded,
    string? ErrorMessage = null);
