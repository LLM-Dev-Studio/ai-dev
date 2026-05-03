namespace AiDev.Features.Planning.Models;

public sealed class PlanningSessionManifest
{
    public string SessionId { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public PlanningPhase CurrentPhase { get; set; } = PlanningPhase.Phase1BusinessDiscovery;

    /// <summary>Phases whose DSL has been locked (immutable). Ordered by phase number.</summary>
    public List<PlanningPhase> LockedPhases { get; init; } = [];

    public bool IsPhase1Locked => LockedPhases.Contains(PlanningPhase.Phase1BusinessDiscovery);
    public bool IsPhase2Locked => LockedPhases.Contains(PlanningPhase.Phase2SolutionShaping);
    public bool IsPhase3Locked => LockedPhases.Contains(PlanningPhase.Phase3PlanningDecomposition);
}
