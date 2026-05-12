namespace AiDev.Features.Workspace;

/// <summary>
/// Represents summary information about a workspace project.
/// </summary>
public sealed class WorkspaceProject
{
    /// <summary>
    /// Creates a project summary with validated identity and normalized optional metadata.
    /// </summary>
    public WorkspaceProject(ProjectSlug slug, string name, string? description = null, DateTime? createdAt = null, int agentCount = 0)
    {
        ArgumentNullException.ThrowIfNull(slug);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required.", nameof(name));
        if (agentCount < 0)
            throw new ArgumentOutOfRangeException(nameof(agentCount));

        Slug = slug;
        Name = name;
        Description = NormalizeOptional(description);
        CreatedAt = createdAt;
        AgentCount = agentCount;
    }

    /// <summary>
    /// Gets the unique project slug.
    /// </summary>
    public ProjectSlug Slug { get; }

    /// <summary>
    /// Gets the display name of the project.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the optional project description.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the project creation timestamp when available.
    /// </summary>
    public DateTime? CreatedAt { get; }

    /// <summary>
    /// Gets the number of agents associated with the project.
    /// </summary>
    public int AgentCount { get; }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
