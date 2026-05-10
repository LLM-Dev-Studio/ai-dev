namespace AiDev.Features.Planning;

/// <summary>
/// Lifecycle state of a planning session.
/// Serialised as the name string (e.g. "Active", "Phase1Locked") for backward compatibility.
/// </summary>
public readonly struct PlanningSessionState : IEquatable<PlanningSessionState>
{
    private readonly string? _key;

    private PlanningSessionState(string key) => _key = key;

    /// <summary>Session is active; conversation is ongoing.</summary>
    public static readonly PlanningSessionState Active       = new("Active");

    /// <summary>Phase 1 is complete and Business.dsl is locked. Phase 2 may begin.</summary>
    public static readonly PlanningSessionState Phase1Locked = new("Phase1Locked");

    /// <summary>Phase 2 is complete and Solution.dsl is locked. Phase 3 may begin.</summary>
    public static readonly PlanningSessionState Phase2Locked = new("Phase2Locked");

    /// <summary>Phase 3 is complete and Plan.dsl has been finalised.</summary>
    public static readonly PlanningSessionState Completed    = new("Completed");

    public bool IsCompleted => Serialize() == "Completed";

    public string Serialize() => _key ?? "Active";

    public static PlanningSessionState Parse(string? value) => value switch
    {
        "Phase1Locked" => Phase1Locked,
        "Phase2Locked" => Phase2Locked,
        "Completed"    => Completed,
        _              => Active,
    };

    public bool Equals(PlanningSessionState other) => Serialize() == other.Serialize();
    public override bool Equals(object? obj) => obj is PlanningSessionState other && Equals(other);
    public override int GetHashCode() => Serialize().GetHashCode();
    public static bool operator ==(PlanningSessionState left, PlanningSessionState right) => left.Equals(right);
    public static bool operator !=(PlanningSessionState left, PlanningSessionState right) => !left.Equals(right);
    public override string ToString() => Serialize();
}
