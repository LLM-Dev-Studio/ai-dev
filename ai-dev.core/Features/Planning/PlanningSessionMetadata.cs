namespace AiDev.Features.Planning;

/// <summary>
/// Persisted metadata for a planning session. Written to <c>metadata.json</c>.
/// </summary>
public sealed class PlanningSessionMetadata
{
    public required string Id { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required SessionPhase CurrentPhase { get; set; }
    public required PlanningSessionState State { get; set; }
    public DateTimeOffset? Phase1LockedAt { get; set; }
    public DateTimeOffset? Phase2LockedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Running total of input tokens consumed during Phase 1 (last API call's input count).</summary>
    public int Phase1InputTokens { get; set; }

    /// <summary>Running total of input tokens consumed during Phase 2 (last API call's input count).</summary>
    public int Phase2InputTokens { get; set; }

    /// <summary>Running total of input tokens consumed during Phase 3 (last API call's input count).</summary>
    public int Phase3InputTokens { get; set; }

    /// <summary>Returns the current phase's running input token count.</summary>
    public int GetCurrentPhaseTokens() => CurrentPhase switch
    {
        SessionPhase.Phase1BusinessDiscovery => Phase1InputTokens,
        SessionPhase.Phase2SolutionShaping   => Phase2InputTokens,
        SessionPhase.Phase3PlanningDecomposition => Phase3InputTokens,
        _ => 0,
    };

    /// <summary>Updates the token count for the given phase.</summary>
    public void SetPhaseTokens(SessionPhase phase, int tokens)
    {
        switch (phase)
        {
            case SessionPhase.Phase1BusinessDiscovery:
                Phase1InputTokens = tokens;
                break;
            case SessionPhase.Phase2SolutionShaping:
                Phase2InputTokens = tokens;
                break;
            case SessionPhase.Phase3PlanningDecomposition:
                Phase3InputTokens = tokens;
                break;
        }
    }
}
