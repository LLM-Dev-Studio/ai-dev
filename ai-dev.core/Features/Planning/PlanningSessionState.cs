namespace AiDev.Features.Planning;

public enum PlanningSessionState
{
    /// <summary>Session is active; conversation is ongoing.</summary>
    Active,

    /// <summary>Phase 1 is complete and Business.dsl is locked. Phase 2 may begin.</summary>
    Phase1Locked,

    /// <summary>Phase 2 is complete and Solution.dsl is locked. Phase 3 may begin.</summary>
    Phase2Locked,

    /// <summary>Phase 3 is complete and Plan.dsl has been finalised.</summary>
    Completed,
}
