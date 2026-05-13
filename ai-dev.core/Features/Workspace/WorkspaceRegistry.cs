namespace AiDev.Features.Workspace;

/// <summary>
/// Represents the persisted registry of workspace projects.
/// </summary>
public class WorkspaceRegistry
{
    /// <summary>
    /// Gets or sets the registered workspace projects.
    /// </summary>
    public List<WorkspaceRegistryEntry> Projects { get; set; } = [];
}
