namespace AiDev.Features.Workspace;

/// <summary>
/// Represents a known codebase in the global project registry.
/// The project's slug and display name are read from the codebase's own
/// <c>.ai-dev/project.json</c> at runtime and are not duplicated here.
/// </summary>
public class WorkspaceRegistryEntry
{
    /// <summary>
    /// Gets or sets the absolute path to the codebase root directory.
    /// </summary>
    public required string Path { get; set; }
}
