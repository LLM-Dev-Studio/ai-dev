namespace AiDev.Features.Workspace;

public class WorkspaceProject
{
    public ProjectSlug Slug { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? CreatedAt { get; set; }
    public int AgentCount { get; set; }
}
