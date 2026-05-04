using AiDev.Executors;

namespace AiDev.Features.Agent;

public interface IAgentRunnerService
{
    bool IsRunning(ProjectSlug projectSlug, AgentSlug agentSlug);
    bool IsRateLimited(ProjectSlug projectSlug, AgentSlug agentSlug);
    IReadOnlyList<RunningSession> GetRunningSessions();
    Task RecoverStaleSessionsAsync(IEnumerable<ProjectSlug> projects);
    bool LaunchAgent(ProjectSlug projectSlug, AgentSlug agentSlug, AgentLaunchTrigger? trigger = null);
    bool StopAgent(ProjectSlug projectSlug, AgentSlug agentSlug);
}
