namespace AiDev.Features.Workspace;

/// <summary>
/// Represents a project entry stored in the workspace registry.
/// </summary>
public class WorkspaceRegistryEntry
{
    /// <summary>
    /// Gets or sets the project slug.
    /// </summary>
    public required string Slug { get; set; }

    /// <summary>
    /// Gets or sets the relative project path.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Gets or sets the display name of the project.
    /// </summary>
    public required string Name { get; set; }
}
