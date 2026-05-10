namespace AiDev.Features.Agent;

/// <summary>
/// Captures the originating cause for an agent session so traces and logs can correlate executor requests back to the triggering action.
/// </summary>
public sealed record AgentLaunchTrigger(
    string Source,
    string Reason,
    ProjectSlug? ProjectSlug = null,
    TaskId? TaskId = null,
    DecisionId? DecisionId = null,
    string? MessageFile = null,
    string? ParentSpanId = null);
