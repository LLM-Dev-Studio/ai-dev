namespace AiDevNet.Services;

public class AgentInfo
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Model { get; set; } = "sonnet";
    public string Status { get; set; } = "idle";
    public string Description { get; set; } = string.Empty;
    public string? LastRunAt { get; set; }
    public int InboxCount { get; set; }
    public string Executor { get; set; } = "claude";
}
