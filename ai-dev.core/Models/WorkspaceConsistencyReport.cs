namespace AiDev.Models;

/// <summary>
/// Represents consistency findings aggregated across workspace projects.
/// </summary>
public sealed class WorkspaceConsistencyReport(IReadOnlyList<ProjectConsistencyReport> projects)
{
    /// <summary>
    /// Gets the project consistency reports included in the workspace report.
    /// </summary>
    public IReadOnlyList<ProjectConsistencyReport> Projects { get; } = projects;

    /// <summary>
    /// Gets the total number of error findings across all projects.
    /// </summary>
    public int ErrorCount => Projects.Sum(p => p.Findings.Count(f => f.Severity == ConsistencySeverity.Error));

    /// <summary>
    /// Gets the total number of warning findings across all projects.
    /// </summary>
    public int WarningCount => Projects.Sum(p => p.Findings.Count(f => f.Severity == ConsistencySeverity.Warning));
}
