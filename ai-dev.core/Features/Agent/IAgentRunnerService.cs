using AiDev.Executors;

namespace AiDev.Features.Agent;

/// <summary>
/// Defines operations for launching, stopping, and inspecting agent runner sessions.
/// </summary>
public interface IAgentRunnerService
{
    /// <summary>
    /// Determines whether the specified agent is currently running.
    /// </summary>
    /// <param name="projectSlug">The project that owns the agent.</param>
    /// <param name="agentSlug">The agent to inspect.</param>
    /// <returns><see langword="true"/> when the agent is running; otherwise, <see langword="false"/>.</returns>
    bool IsRunning(ProjectSlug projectSlug, AgentSlug agentSlug);

    /// <summary>
    /// Determines whether the specified agent is currently rate limited.
    /// </summary>
    /// <param name="projectSlug">The project that owns the agent.</param>
    /// <param name="agentSlug">The agent to inspect.</param>
    /// <returns><see langword="true"/> when the agent is rate limited; otherwise, <see langword="false"/>.</returns>
    bool IsRateLimited(ProjectSlug projectSlug, AgentSlug agentSlug);

    /// <summary>
    /// Gets the currently running agent sessions.
    /// </summary>
    /// <returns>The active running sessions.</returns>
    IReadOnlyList<RunningSession> GetRunningSessions();

    /// <summary>
    /// Attempts to recover stale sessions for the specified projects.
    /// </summary>
    /// <param name="projects">The projects whose sessions should be inspected.</param>
    /// <returns>A task that completes when stale session recovery finishes.</returns>
    Task RecoverStaleSessionsAsync(IEnumerable<ProjectSlug> projects);

    /// <summary>
    /// Launches the specified agent.
    /// </summary>
    /// <param name="projectSlug">The project that owns the agent.</param>
    /// <param name="agentSlug">The agent to launch.</param>
    /// <param name="trigger">The optional launch trigger metadata.</param>
    /// <returns><see langword="true"/> when the agent was launched; otherwise, <see langword="false"/>.</returns>
    bool LaunchAgent(ProjectSlug projectSlug, AgentSlug agentSlug, AgentLaunchTrigger? trigger = null);

    /// <summary>
    /// Stops the specified agent.
    /// </summary>
    /// <param name="projectSlug">The project that owns the agent.</param>
    /// <param name="agentSlug">The agent to stop.</param>
    /// <returns><see langword="true"/> when the agent was stopped; otherwise, <see langword="false"/>.</returns>
    bool StopAgent(ProjectSlug projectSlug, AgentSlug agentSlug);
}
