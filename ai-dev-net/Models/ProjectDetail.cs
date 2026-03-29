namespace AiDevNet.Models;

public class ProjectDetail
{
    public ProjectSlug Slug { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? CodebasePath { get; set; }
    public DateTime? CreatedAt { get; set; }
}
