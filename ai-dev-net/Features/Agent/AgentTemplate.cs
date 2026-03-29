namespace AiDevNet.Features.Agent;

public class AgentTemplate
{
    public AgentSlug Slug { get; set; } = null!;
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string Model { get; set; } = "sonnet";
    public string Description { get; set; } = "";
    public string Content { get; set; } = "";
}
