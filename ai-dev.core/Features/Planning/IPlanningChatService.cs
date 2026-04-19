namespace AiDev.Features.Planning;

/// <summary>
/// Provides LLM-backed conversation turns for the Planning Tasks Screen.
/// The LLM client used is resolved from the analyst agent's configured executor for the project.
/// All three phases are implemented: Phase 1 (Business Discovery), Phase 2 (Solution Shaping),
/// and Phase 3 (Planning &amp; Decomposition).
/// </summary>
public interface IPlanningChatService
{
    Task<PlanningChatResponse> SendPhase1MessageAsync(
        ProjectSlug projectSlug,
        IReadOnlyList<ConversationTurn> history,
        string userMessage,
        CancellationToken ct = default);

    Task<PlanningChatResponse> SendPhase2MessageAsync(
        ProjectSlug projectSlug,
        IReadOnlyList<ConversationTurn> history,
        string businessDsl,
        string userMessage,
        CancellationToken ct = default);

    Task<PlanningChatResponse> SendPhase3MessageAsync(
        ProjectSlug projectSlug,
        IReadOnlyList<ConversationTurn> history,
        string businessDsl,
        string solutionDsl,
        string userMessage,
        CancellationToken ct = default);

    Task<string> GenerateBusinessDslAsync(
        ProjectSlug projectSlug,
        IReadOnlyList<ConversationTurn> history,
        CancellationToken ct = default);

    /// <summary>
    /// The caller must validate the result using <see cref="SolutionDslValidator"/> before persisting.
    /// </summary>
    Task<string> GenerateSolutionDslAsync(
        ProjectSlug projectSlug,
        IReadOnlyList<ConversationTurn> history,
        string businessDsl,
        CancellationToken ct = default);

    Task<string> GeneratePlanDslAsync(
        ProjectSlug projectSlug,
        IReadOnlyList<ConversationTurn> history,
        string businessDsl,
        string solutionDsl,
        CancellationToken ct = default);
}
