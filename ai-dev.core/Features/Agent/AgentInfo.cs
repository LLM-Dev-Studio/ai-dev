using AiDev.Executors;

namespace AiDev.Features.Agent;

public class AgentInfo
{
    public required AgentSlug Slug { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Model { get; set; } = "sonnet";
    public AgentStatus Status { get; set; } = AgentStatus.Idle;
    public string Description { get; set; } = string.Empty;
    public DateTime? LastRunAt { get; set; }
    public int InboxCount { get; set; }
    public string Executor { get; set; } = IAgentExecutor.Default;

    /// <summary>
    /// Skill keys enabled for this agent (e.g. ["git-read", "git-write"]).
    /// Empty means the executor uses its own defaults (preserves behaviour for existing agents).
    /// </summary>
    public IReadOnlyList<string> Skills { get; set; } = [];
}
