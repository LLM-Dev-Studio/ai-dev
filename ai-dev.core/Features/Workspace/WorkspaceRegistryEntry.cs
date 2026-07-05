namespace AiDev.Features.Workspace;

/// <summary>
/// Represents a known codebase in the global managed-projects file.
/// The slug is stored alongside the path so listing projects does not require
/// reading every <c>.ai-dev/project.json</c> just to obtain the identifier.
/// </summary>
public class WorkspaceRegistryEntry
{
    /// <summary>
    /// Gets or sets the absolute path to the codebase root directory.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Gets or sets the project slug. Populated when the project is registered;
    /// may be <see langword="null"/> for entries migrated from older registry files.
    /// </summary>
    public string? Slug { get; set; }
}
