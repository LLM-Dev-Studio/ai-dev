namespace AiDev.Features.Planning.Models;

/// <summary>
/// Represents persisted metadata for a planning session.
/// </summary>
public sealed class PlanningSessionManifest
{
    /// <summary>
    /// Gets the planning session identifier.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the project name associated with the session.
    /// </summary>
    public string ProjectName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when the session was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets or sets the current planning phase.
    /// </summary>
    public PlanningPhase CurrentPhase { get; set; } = PlanningPhase.Phase1BusinessDiscovery;

    /// <summary>
    /// Gets the phases whose DSL has been locked in read-only form, ordered by phase number.
    /// </summary>
    public List<PlanningPhase> LockedPhases { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether phase 1 has been locked.
    /// </summary>
    public bool IsPhase1Locked => LockedPhases.Contains(PlanningPhase.Phase1BusinessDiscovery);

    /// <summary>
    /// Gets a value indicating whether phase 2 has been locked.
    /// </summary>
    public bool IsPhase2Locked => LockedPhases.Contains(PlanningPhase.Phase2SolutionShaping);

    /// <summary>
    /// Gets a value indicating whether phase 3 has been locked.
    /// </summary>
    public bool IsPhase3Locked => LockedPhases.Contains(PlanningPhase.Phase3PlanningDecomposition);
}
