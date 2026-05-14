namespace AiDev.Features.Workspace;

/// <summary>
/// Represents the global registry of known project codebases.
/// </summary>
public class WorkspaceRegistry
{
    /// <summary>
    /// Gets or sets the registered codebase paths.
    /// </summary>
    public List<WorkspaceRegistryEntry> Projects { get; set; } = [];

    /// <summary>
    /// Gets or sets the absolute codebase path of the most recently activated project.
    /// </summary>
    public string? LastActivePath { get; set; }
}
