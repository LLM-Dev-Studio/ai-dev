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
    TokenUsage? GetLastSessionUsage(ProjectSlug projectSlug, AgentSlug agentSlug);
    TokenUsage? GetSessionUsage(ProjectSlug projectSlug, AgentSlug agentSlug, TranscriptDate date);
    Result<Unit> WriteInboxMessage(ProjectSlug projectSlug, AgentSlug agentSlug,
        string from, string re, string type, string priority, string body,
        TaskId? taskId = null, string? decisionId = null);
}
