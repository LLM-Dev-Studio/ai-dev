namespace AiDev.Models;

public class ProjectDetail
{
    public required ProjectSlug Slug { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public string? CodebasePath { get; set; }
    public DateTime? CreatedAt { get; set; }
}
