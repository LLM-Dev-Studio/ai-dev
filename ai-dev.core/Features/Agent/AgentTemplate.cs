using AiDev.Executors;

namespace AiDev.Features.Agent;

public class AgentTemplate
{
    public AgentSlug Slug { get; set; } = null!;
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string Model { get; set; } = "claude-sonnet-4-6";
    public string? Executor { get; set; }
    public List<string> Skills { get; set; } = [];
    public string Description { get; set; } = "";
    public string Content { get; set; } = "";
    public ThinkingLevel ThinkingLevel { get; set; } = ThinkingLevel.Off;
}
