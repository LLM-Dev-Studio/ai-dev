namespace AiDev.Models;

public enum ConsistencySeverity
{
    Info,
    Warning,
    Error,
}

public enum ConsistencyFixType
{
    None,
    AutoRepaired,
    ManualActionRequired,
}

public sealed record ConsistencyFinding(
    string Code,
    ConsistencySeverity Severity,
    string Message,
    ConsistencyFixType FixType = ConsistencyFixType.None,
    string? ProjectSlug = null,
    string? ResourceId = null);

public sealed class ProjectConsistencyReport(ProjectSlug projectSlug, IReadOnlyList<ConsistencyFinding> findings)
{
    public ProjectSlug ProjectSlug { get; } = projectSlug;
    public IReadOnlyList<ConsistencyFinding> Findings { get; } = findings;

    public bool HasErrors => Findings.Any(f => f.Severity == ConsistencySeverity.Error);
    public bool HasWarnings => Findings.Any(f => f.Severity == ConsistencySeverity.Warning);
}

public sealed class WorkspaceConsistencyReport(IReadOnlyList<ProjectConsistencyReport> projects)
{
    public IReadOnlyList<ProjectConsistencyReport> Projects { get; } = projects;

    public int ErrorCount => Projects.Sum(p => p.Findings.Count(f => f.Severity == ConsistencySeverity.Error));
    public int WarningCount => Projects.Sum(p => p.Findings.Count(f => f.Severity == ConsistencySeverity.Warning));
}
