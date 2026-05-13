using System.Text.Json.Serialization;

namespace AiDev.Features.Planning;

/// <summary>
/// The current active phase of a planning session.
/// Serialised as the name string for backward compatibility with existing session files.
/// </summary>
[JsonConverter(typeof(SessionPhaseJsonConverter))]
public readonly struct SessionPhase : IEquatable<SessionPhase>
{
    private readonly string? _key;

    private SessionPhase(
        string key,
        string dslFileName,
        string displayTitle,
        string sidebarTitle,
        string roleBadge,
        string exportBaseName,
        string reviewTitle,
        string shortLabel)
    {
        _key           = key;
        DslFileName    = dslFileName;
        DisplayTitle   = displayTitle;
        SidebarTitle   = sidebarTitle;
        RoleBadge      = roleBadge;
        ExportBaseName = exportBaseName;
        ReviewTitle    = reviewTitle;
        ShortLabel     = shortLabel;
    }

    /// <summary>
    /// Represents phase 1 business discovery.
    /// </summary>
    public static readonly SessionPhase Phase1BusinessDiscovery     = new(
        key:           "Phase1BusinessDiscovery",
        dslFileName:   FilePathConstants.BusinessDslFileName,
        displayTitle:  "Phase 1: Business Discovery",
        sidebarTitle:  "Phase 1 — Business Discovery",
        roleBadge:     "Role: Business Analyst",
        exportBaseName:"business",
        reviewTitle:   "Phase 1 — Business Discovery (Read-Only)",
        shortLabel:    "Ph1");

    /// <summary>
    /// Represents phase 2 solution shaping.
    /// </summary>
    public static readonly SessionPhase Phase2SolutionShaping       = new(
        key:           "Phase2SolutionShaping",
        dslFileName:   FilePathConstants.SolutionDslFileName,
        displayTitle:  "Phase 2: Solution Shaping",
        sidebarTitle:  "Phase 2 — Solution Shaping",
        roleBadge:     "Role: Solution Architect",
        exportBaseName:"solution",
        reviewTitle:   "Phase 2 — Solution Shaping (Read-Only)",
        shortLabel:    "Ph2");

    /// <summary>
    /// Represents phase 3 planning and decomposition.
    /// </summary>
    public static readonly SessionPhase Phase3PlanningDecomposition = new(
        key:           "Phase3PlanningDecomposition",
        dslFileName:   FilePathConstants.PlanDslFileName,
        displayTitle:  "Phase 3: Planning & Decomposition",
        sidebarTitle:  "Phase 3 — Planning & Decomposition",
        roleBadge:     "Role: Planning Assistant",
        exportBaseName:"plan",
        reviewTitle:   "Phase 3 — Planning & Decomposition (Read-Only)",
        shortLabel:    "Ph3");

    /// <summary>Filename of the DSL artefact owned by this phase (e.g. "Business.dsl").</summary>
    public string DslFileName    { get; }

    /// <summary>User-facing phase title used in the DSL panel header (e.g. "Phase 1: Business Discovery").</summary>
    public string DisplayTitle   { get; }

    /// <summary>Phase title used in the page sidebar and header area (em-dash format).</summary>
    public string SidebarTitle   { get; }

    /// <summary>Role badge text shown in the phase header (e.g. "Role: Business Analyst").</summary>
    public string RoleBadge      { get; }

    /// <summary>Base filename suggested when exporting the DSL (e.g. "business").</summary>
    public string ExportBaseName { get; }

    /// <summary>User-facing phase title used in the read-only review modal.</summary>
    public string ReviewTitle    { get; }

    /// <summary>Compact label used in the session list (e.g. "Ph1").</summary>
    public string ShortLabel     { get; }

    /// <summary>
    /// Serializes the phase to its persisted key.
    /// </summary>
    /// <returns>The serialized phase key.</returns>
    public string Serialize() => _key ?? "Phase1BusinessDiscovery";

    /// <summary>
    /// Parses a persisted phase key.
    /// </summary>
    /// <param name="value">The serialized phase key.</param>
    /// <returns>The parsed phase, defaulting to phase 1 when the value is unknown.</returns>
    public static SessionPhase Parse(string? value) => value switch
    {
        "Phase1BusinessDiscovery"     => Phase1BusinessDiscovery,
        "Phase2SolutionShaping"       => Phase2SolutionShaping,
        "Phase3PlanningDecomposition" => Phase3PlanningDecomposition,
        _                             => Phase1BusinessDiscovery,
    };

    public bool Equals(SessionPhase other) => Serialize() == other.Serialize();
    public override bool Equals(object? obj) => obj is SessionPhase other && Equals(other);
    public override int GetHashCode() => Serialize().GetHashCode();
    public static bool operator ==(SessionPhase left, SessionPhase right) => left.Equals(right);
    public static bool operator !=(SessionPhase left, SessionPhase right) => !left.Equals(right);
    public override string ToString() => Serialize();
}

internal sealed class SessionPhaseJsonConverter : JsonConverter<SessionPhase>
{
    public override SessionPhase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => SessionPhase.Parse(reader.GetString());

    public override void Write(Utf8JsonWriter writer, SessionPhase value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Serialize());
}
