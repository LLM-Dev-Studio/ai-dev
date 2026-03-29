namespace AiDev.Features.Agent;

public class AgentActivityItem
{
    public string AgentSlug { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public int MessagesSent { get; set; }
    public int MessagesReceived { get; set; }
}
