namespace AiDev.Features.Agent;

public class AgentActivityItem
{
    public AgentSlug AgentSlug { get; set; } = new("unnamed");
    public string AgentName { get; set; } = string.Empty;
    public AgentExecutorName? Executor { get; set; }
    public string Model { get; set; } = string.Empty;
    public int MessagesSent { get; set; }
    public int MessagesReceived { get; set; }
}
