namespace AiDev.Models;

/// <summary>
/// Represents detailed metadata for a project.
/// </summary>
public class ProjectDetail
{
    /// <summary>
    /// Gets or sets the project slug.
    /// </summary>
    public required ProjectSlug Slug { get; set; }

    /// <summary>
    /// Gets or sets the project display name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the project description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the optional codebase path.
    /// </summary>
    public string? CodebasePath { get; set; }

    /// <summary>
    /// Gets or sets the project creation timestamp.
    /// </summary>
    public DateTime? CreatedAt { get; set; }
}
