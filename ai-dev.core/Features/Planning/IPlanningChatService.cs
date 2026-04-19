namespace AiDev.Features.Planning;

/// <summary>
/// Provides LLM-backed conversation turns for the Planning Tasks Screen.
/// All three phases are implemented: Phase 1 (Business Discovery), Phase 2 (Solution Shaping),
/// and Phase 3 (Planning &amp; Decomposition).
/// </summary>
public interface IPlanningChatService
{
    /// <summary>
    /// Sends a Phase 1 (Business Discovery) user message to the LLM.
    /// Applies the EC-6 implementation-detail keyword filter to the response.
    /// </summary>
    /// <param name="history">Full conversation history for Phase 1 (used to build the messages array).</param>
    /// <param name="userMessage">The new user message to send.</param>
    Task<PlanningChatResponse> SendPhase1MessageAsync(
        IReadOnlyList<ConversationTurn> history,
        string userMessage,
        CancellationToken ct = default);

    /// <summary>
    /// Sends a Phase 3 (Planning &amp; Decomposition) user message to the LLM.
    /// Injects locked Business.dsl and Solution.dsl as read-only context in the system prompt.
    /// </summary>
    /// <param name="history">Full conversation history for Phase 3.</param>
    /// <param name="businessDsl">Locked Business.dsl content from Phase 1.</param>
    /// <param name="solutionDsl">Locked Solution.dsl content from Phase 2.</param>
    /// <param name="userMessage">The new user message to send.</param>
    Task<PlanningChatResponse> SendPhase3MessageAsync(
        IReadOnlyList<ConversationTurn> history,
        string businessDsl,
        string solutionDsl,
        string userMessage,
        CancellationToken ct = default);

    /// <summary>
    /// Sends a Phase 2 (Solution Shaping) user message to the LLM.
    /// The LLM acts as a Solution Architect constrained to the VSA stack taxonomy.
    /// Injects locked Business.dsl as read-only context.
    /// </summary>
    /// <param name="history">Full conversation history for Phase 2.</param>
    /// <param name="businessDsl">Locked Business.dsl content from Phase 1.</param>
    /// <param name="userMessage">The new user message to send.</param>
    Task<PlanningChatResponse> SendPhase2MessageAsync(
        IReadOnlyList<ConversationTurn> history,
        string businessDsl,
        string userMessage,
        CancellationToken ct = default);

    /// <summary>
    /// Asks the LLM to generate a Solution.dsl YAML document from the Phase 2 conversation history.
    /// Returns the raw YAML string suitable for validation and locking.
    /// The caller must validate the result using <see cref="SolutionDslValidator"/> before persisting.
    /// </summary>
    Task<string> GenerateSolutionDslAsync(
        IReadOnlyList<ConversationTurn> history,
        string businessDsl,
        CancellationToken ct = default);

    /// <summary>
    /// Asks the LLM to generate a Business.dsl YAML document from the Phase 1 conversation history.
    /// Returns the raw YAML string suitable for review and locking.
    /// </summary>
    Task<string> GenerateBusinessDslAsync(
        IReadOnlyList<ConversationTurn> history,
        CancellationToken ct = default);

    /// <summary>
    /// Asks the LLM to generate a Plan.dsl YAML document from the Phase 3 conversation history.
    /// Injects Business.dsl and Solution.dsl as context.
    /// Returns the raw YAML string suitable for review and finalisation.
    /// </summary>
    Task<string> GeneratePlanDslAsync(
        IReadOnlyList<ConversationTurn> history,
        string businessDsl,
        string solutionDsl,
        CancellationToken ct = default);
}
