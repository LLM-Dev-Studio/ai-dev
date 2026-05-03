using AiDev.Models;

namespace AiDev.Features.Planning;

/// <summary>
/// Manages the lifecycle of planning sessions including session creation, conversation persistence,
/// phase transitions, and DSL file management.
/// </summary>
public interface IPlanningSessionService
{
    /// <summary>Creates a new planning session for the given project and persists its initial metadata.</summary>
    Task<PlanningSessionMetadata> CreateSessionAsync(ProjectSlug projectSlug, CancellationToken ct = default);

    /// <summary>Returns the most recent active (not completed) session for the project, or null if none exists.</summary>
    PlanningSessionMetadata? GetActiveSession(ProjectSlug projectSlug);

    /// <summary>Returns metadata for a specific session, or null if not found.</summary>
    PlanningSessionMetadata? GetSession(ProjectSlug projectSlug, string sessionId);

    /// <summary>Returns all session metadata entries for the project, ordered by creation date descending.</summary>
    IReadOnlyList<PlanningSessionMetadata> ListSessions(ProjectSlug projectSlug);

    /// <summary>Returns all conversation turns for the session (all phases).</summary>
    IReadOnlyList<ConversationTurn> GetConversation(ProjectSlug projectSlug, string sessionId);

    /// <summary>Returns conversation turns for a specific phase.</summary>
    IReadOnlyList<ConversationTurn> GetConversationForPhase(ProjectSlug projectSlug, string sessionId, SessionPhase phase);

    /// <summary>Appends a single conversation turn to conversation.jsonl.</summary>
    Task AppendTurnAsync(ProjectSlug projectSlug, string sessionId, ConversationTurn turn, CancellationToken ct = default);

    /// <summary>Saves a draft DSL file (Business.dsl / Solution.dsl / Plan.dsl) to the drafts/ subdirectory.</summary>
    Task SaveDraftDslAsync(ProjectSlug projectSlug, string sessionId, SessionPhase phase, string yamlContent, CancellationToken ct = default);

    /// <summary>
    /// Locks a phase by writing the DSL to the root session directory and updating session state.
    /// Returns an error if the phase is already locked or if required DSL fields are missing.
    /// </summary>
    Task<Result<Unit>> LockPhaseAsync(ProjectSlug projectSlug, string sessionId, SessionPhase phase, string yamlContent, CancellationToken ct = default);

    /// <summary>Updates the token count for the current phase in session metadata.</summary>
    Task UpdateTokenCountAsync(ProjectSlug projectSlug, string sessionId, SessionPhase phase, int inputTokens, CancellationToken ct = default);

    /// <summary>Returns the locked DSL content for a phase, or null if not yet locked.</summary>
    string? GetLockedDsl(ProjectSlug projectSlug, string sessionId, SessionPhase phase);

    /// <summary>Returns the draft DSL content for a phase, or null if no draft exists.</summary>
    string? GetDraftDsl(ProjectSlug projectSlug, string sessionId, SessionPhase phase);

    /// <summary>
    /// Creates an EC-4 escalation decision file in the project's decisions/pending/ directory.
    /// Called when no VSA-compatible solution can be proposed for the user's requirements (AD-11).
    /// Returns the path of the created decision file.
    /// </summary>
    Task<string> CreateEc4EscalationAsync(
        ProjectSlug projectSlug,
        string sessionId,
        string unsupportedRequirement,
        string closestAlternative,
        CancellationToken ct = default);
}
