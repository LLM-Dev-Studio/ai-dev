namespace AiDev.Features.Planning.Models;

/// <summary>
/// Represents the business-to-implementation planning phases.
/// </summary>
public enum PlanningPhase
{
    /// <summary>
    /// Focuses on business discovery and requirement clarification.
    /// </summary>
    Phase1BusinessDiscovery = 1,

    /// <summary>
    /// Focuses on shaping the candidate solution.
    /// </summary>
    Phase2SolutionShaping = 2,

    /// <summary>
    /// Focuses on decomposing the solution into implementation work.
    /// </summary>
    Phase3PlanningDecomposition = 3,
}
