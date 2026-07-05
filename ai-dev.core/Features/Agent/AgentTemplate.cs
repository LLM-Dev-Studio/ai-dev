using AiDev.Executors;

namespace AiDev.Features.Agent;

/// <summary>
/// Represents an agent template and its editable metadata.
/// </summary>
public class AgentTemplate
{
    /// <summary>Gets or sets the template slug.</summary>
    public AgentSlug Slug { get; set; } = null!;
    /// <summary>Gets or sets the template display name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Gets or sets the template role.</summary>
    public string Role { get; set; } = "";
    /// <summary>Gets or sets the default model identifier.</summary>
    public string Model { get; set; } = "claude-sonnet-4-6";
    /// <summary>Gets or sets the optional executor identifier.</summary>
    public string? Executor { get; set; }
    /// <summary>Gets or sets the configured skills.</summary>
    public List<string> Skills { get; set; } = [];
    /// <summary>Gets or sets the template description.</summary>
    public string Description { get; set; } = "";
    /// <summary>Gets or sets the main template content.</summary>
    public string Content { get; set; } = "";
    /// <summary>Gets or sets the compact template content.</summary>
    public string CompactContent { get; set; } = "";
    /// <summary>Gets or sets the default thinking level.</summary>
    public ThinkingLevel ThinkingLevel { get; set; }
}
