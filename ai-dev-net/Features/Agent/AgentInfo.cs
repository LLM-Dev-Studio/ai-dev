using AiDevNet.Models.Types;

namespace AiDevNet.Features.Agent;

public class AgentInfo
{
    public required Models.AgentSlug Slug { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Model { get; set; } = "sonnet";
    public AgentStatus Status { get; set; } = AgentStatus.Idle;
    public string Description { get; set; } = string.Empty;
    public DateTime? LastRunAt { get; set; }
    public int InboxCount { get; set; }
    public string Executor { get; set; } = IAgentExecutor.Default;
}
