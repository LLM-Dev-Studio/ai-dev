namespace AiDev.Models;

/// <summary>
/// Represents the consistency findings for a single project.
/// </summary>
public sealed class ProjectConsistencyReport(ProjectSlug projectSlug, IReadOnlyList<ConsistencyFinding> findings)
{
    /// <summary>
    /// Gets the project slug associated with the report.
    /// </summary>
    public ProjectSlug ProjectSlug { get; } = projectSlug;

    /// <summary>
    /// Gets the findings reported for the project.
    /// </summary>
    public IReadOnlyList<ConsistencyFinding> Findings { get; } = findings;

    /// <summary>
    /// Gets a value indicating whether the report contains any error findings.
    /// </summary>
    public bool HasErrors => Findings.Any(f => f.Severity == ConsistencySeverity.Error);

    /// <summary>
    /// Gets a value indicating whether the report contains any warning findings.
    /// </summary>
    public bool HasWarnings => Findings.Any(f => f.Severity == ConsistencySeverity.Warning);
}
