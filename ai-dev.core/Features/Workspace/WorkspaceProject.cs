namespace AiDev.Features.Workspace;

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

    public ProjectSlug Slug { get; }
    public string Name { get; }
    public string? Description { get; }
    public DateTime? CreatedAt { get; }
    public int AgentCount { get; }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
