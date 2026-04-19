using AiDev.Executors;
using AiDev.Features.Agent;
using AiDev.Features.Planning.Models;

using Microsoft.Extensions.Logging;

namespace AiDev.Features.Planning;

/// <summary>
/// Implements <see cref="IPlanningChatService"/> using whichever <see cref="IPlanningLlmClient"/>
/// matches the analyst agent's configured executor for the project.
/// Falls back to the "anthropic" client if no analyst is found or no matching client is registered.
/// </summary>
public sealed class PlanningChatService(
    AgentService agentService,
    IEnumerable<IPlanningLlmClient> llmClients,
    ILogger<PlanningChatService> logger) : IPlanningChatService
{
    private const string FallbackExecutor = AgentExecutorName.AnthropicValue;
    private const string DefaultModel     = "claude-haiku-4-5-20251001";

    // -------------------------------------------------------------------------
    // Phase 1 — Business Discovery
    // -------------------------------------------------------------------------

    public async Task<PlanningChatResponse> SendPhase1MessageAsync(
        ProjectSlug projectSlug,
        IReadOnlyList<ConversationTurn> history,
        string userMessage,
        CancellationToken ct = default)
    {
        var (client, modelId) = Resolve(projectSlug);
        var result = await client.ChatAsync(modelId, Phase1SystemPrompt, ToMessages(history), userMessage, ct)
            .ConfigureAwait(false);

        var firstMatch  = PlanningKeywordBlocklist.FindFirstMatch(result.Content);
        var wasFiltered = firstMatch != null;
        var tokenStatus = TokenStatus.From(result.InputTokens);

        if (wasFiltered)
        {
            logger.LogInformation("[planning-chat] EC-6 filter triggered — term: {Term}", firstMatch);
            const string blockedMessage =
                "I notice I was about to mention some technical implementation details, which aren't appropriate for this phase. " +
                "Let's keep our focus on the business problem. Could you rephrase your last message in purely business terms? " +
                "For example, instead of naming specific technologies, describe what the system needs to do for the users.";
            return new PlanningChatResponse(blockedMessage, WasFiltered: true, [firstMatch!],
                result.InputTokens, result.OutputTokens, tokenStatus);
        }

        return new PlanningChatResponse(result.Content, WasFiltered: false, [],
            result.InputTokens, result.OutputTokens, tokenStatus);
    }

    public async Task<string> GenerateBusinessDslAsync(
        ProjectSlug projectSlug,
        IReadOnlyList<ConversationTurn> history,
        CancellationToken ct = default)
    {
        var (client, modelId) = Resolve(projectSlug);
        var result = await client.ChatAsync(modelId, Phase1SystemPrompt, ToMessages(history),
            BusinessDslGenerationPrompt, ct).ConfigureAwait(false);
        return result.Content;
    }

    // -------------------------------------------------------------------------
    // Phase 2 — Solution Shaping
    // -------------------------------------------------------------------------

    public async Task<PlanningChatResponse> SendPhase2MessageAsync(
        ProjectSlug projectSlug,
        IReadOnlyList<ConversationTurn> history,
        string businessDsl,
        string userMessage,
        CancellationToken ct = default)
    {
        var (client, modelId) = Resolve(projectSlug);
        var result = await client.ChatAsync(modelId, BuildPhase2SystemPrompt(businessDsl),
            ToMessages(history), userMessage, ct).ConfigureAwait(false);
        return new PlanningChatResponse(result.Content, WasFiltered: false, [],
            result.InputTokens, result.OutputTokens, TokenStatus.From(result.InputTokens));
    }

    public async Task<string> GenerateSolutionDslAsync(
        ProjectSlug projectSlug,
        IReadOnlyList<ConversationTurn> history,
        string businessDsl,
        CancellationToken ct = default)
    {
        var (client, modelId) = Resolve(projectSlug);
        var result = await client.ChatAsync(modelId, BuildPhase2SystemPrompt(businessDsl),
            ToMessages(history), SolutionDslGenerationPrompt, ct).ConfigureAwait(false);
        return result.Content;
    }

    // -------------------------------------------------------------------------
    // Phase 3 — Planning & Decomposition
    // -------------------------------------------------------------------------

    public async Task<PlanningChatResponse> SendPhase3MessageAsync(
        ProjectSlug projectSlug,
        IReadOnlyList<ConversationTurn> history,
        string businessDsl,
        string solutionDsl,
        string userMessage,
        CancellationToken ct = default)
    {
        var (client, modelId) = Resolve(projectSlug);
        var result = await client.ChatAsync(modelId, BuildPhase3SystemPrompt(businessDsl, solutionDsl),
            ToMessages(history), userMessage, ct).ConfigureAwait(false);
        return new PlanningChatResponse(result.Content, WasFiltered: false, [],
            result.InputTokens, result.OutputTokens, TokenStatus.From(result.InputTokens));
    }

    public async Task<string> GeneratePlanDslAsync(
        ProjectSlug projectSlug,
        IReadOnlyList<ConversationTurn> history,
        string businessDsl,
        string solutionDsl,
        CancellationToken ct = default)
    {
        var (client, modelId) = Resolve(projectSlug);
        var result = await client.ChatAsync(modelId, BuildPhase3SystemPrompt(businessDsl, solutionDsl),
            ToMessages(history), PlanDslGenerationPrompt, ct).ConfigureAwait(false);
        return result.Content;
    }

    // -------------------------------------------------------------------------
    // Resolution
    // -------------------------------------------------------------------------

    private (IPlanningLlmClient Client, string ModelId) Resolve(ProjectSlug projectSlug)
    {
        var clientMap = llmClients.ToDictionary(c => c.ExecutorName, StringComparer.OrdinalIgnoreCase);

        var analyst = agentService.ListAgents(projectSlug)
            .FirstOrDefault(a => a.Slug.Value.StartsWith("analyst", StringComparison.OrdinalIgnoreCase));

        var executorName = analyst?.Executor?.Value ?? FallbackExecutor;
        var modelId      = analyst?.Model ?? DefaultModel;

        if (!clientMap.TryGetValue(executorName, out var client))
        {
            logger.LogWarning(
                "[planning-chat] No IPlanningLlmClient registered for executor '{Executor}'. " +
                "Falling back to '{Fallback}'.", executorName, FallbackExecutor);

            if (!clientMap.TryGetValue(FallbackExecutor, out client))
                throw new InvalidOperationException(
                    $"No IPlanningLlmClient registered for executor '{executorName}' and the " +
                    $"'{FallbackExecutor}' fallback is also unavailable.");
        }

        logger.LogDebug("[planning-chat] Using executor '{Executor}' model '{Model}'", executorName, modelId);
        return (client, modelId);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IReadOnlyList<ConversationMessage> ToMessages(IReadOnlyList<ConversationTurn> turns) =>
        turns.Select(t => new ConversationMessage
        {
            Role    = t.Role == ConversationRole.User ? "user" : "assistant",
            Content = t.Content,
        }).ToList();

    // -------------------------------------------------------------------------
    // System prompts
    // -------------------------------------------------------------------------

    private const string Phase1SystemPrompt = """
        You are a business analyst facilitating a software requirements discovery conversation.

        Your role is to help the user articulate their software project's business intent, goals,
        constraints, and non-goals — without introducing any technical or implementation considerations.

        DEFINITION OF IMPLEMENTATION DETAIL:
        An implementation detail is any reference to specific technologies, frameworks, libraries,
        programming languages, hosting platforms, cloud providers, database engines, API protocols,
        design patterns, or system architecture constructs (e.g. microservices, event sourcing,
        message queues).

        Business terminology (e.g. user, invoice, approval workflow, customer, stakeholder) is NOT
        an implementation detail. Domain concepts and user roles are NOT implementation details.

        YOUR INSTRUCTIONS:
        - Ask clarifying questions to uncover goals, constraints, and non-goals
        - Challenge vague or ambiguous statements
        - Identify and surface contradictions in the user's statements
        - Summarise stated intent in plain language at appropriate intervals (at minimum once before
          Phase 1 completion is offered)
        - NEVER mention implementation details as defined above
        - NEVER suggest technologies, frameworks, databases, or system architecture
        - If the user mentions implementation details, politely redirect them to business terms
        - Identify and surface contradictions between stated goals and non-goals
        - When you assess that sufficient business intent has been captured, offer to generate the
          Business.dsl summary by saying "I believe we have enough to generate your Business.dsl.
          Would you like me to proceed?"

        Keep your responses focused, professional, and business-oriented.
        """;

    private static string BuildPhase2SystemPrompt(string businessDsl) => $"""
        You are a Solution Architect helping shape a technical solution for a software project.
        Your role is to propose solution options using ONLY the supported VSA (Vertical Slice Architecture)
        stack described below. You must refuse requests for unsupported technologies.

        LOCKED BUSINESS.DSL (read-only context — do not allow modifications):
        ```yaml
        {businessDsl}
        ```

        ---

        SUPPORTED VSA STACK (you MUST constrain all proposals to these options):

        PROJECT TYPES (use exactly these values):
        - API          — ASP.NET Core minimal-API or controller-based REST service
        - Infrastructure — Class library for data persistence, external integrations, repositories
        - SharedContractsSDK — Portable class library with DTOs and interfaces; no business logic, no dependencies
        - MauiHybridUI — .NET MAUI shell hosting Blazor Hybrid web view; UI only, calls API over HTTP
        - Worker       — ASP.NET Core hosted service or Worker Service for background processing

        CROSS-CUTTING MODULES (use exactly these values):
        - Auth          — JWT bearer tokens, ASP.NET Core Identity, role/policy authorisation (API only)
        - CQRS          — Command/Query Responsibility Segregation via MediatR (API, Worker)
        - EFCore        — Entity Framework Core with SQL Server, migrations (Infrastructure only)
        - Observability — Serilog, OpenTelemetry, health checks (API, Worker, Infrastructure)
        - Validation    — FluentValidation with MediatR pipeline or endpoint filters (API, Worker)
        - Caching       — IDistributedCache (Redis/SQL Server), cache-aside (API, Infrastructure)
        - Messaging     — MassTransit consumers/producers (Worker, Infrastructure)

        COMPATIBILITY RULES (enforce these strictly):
        - MauiHybridUI: NO modules permitted. UI communicates exclusively with the API over HTTP.
        - SharedContractsSDK: NO modules permitted. Must remain dependency-free.
        - EFCore: ONLY valid for Infrastructure. Never suggest EFCore for API or Worker.
        - Auth: ONLY valid for API. Workers use service-account credentials configured externally.
        - CQRS: Valid for API and Worker. Strongly recommend pairing with Validation.
        - Observability: Valid for API, Worker, Infrastructure (any combination).
        - Validation: Valid for API and Worker.
        - Caching: Valid for API and Infrastructure.
        - Messaging: Valid for Worker and Infrastructure.
        - Single Infrastructure per solution: Multiple persistence concerns use separate DbContext classes, not separate projects.

        ---

        REFUSAL RULES (for unsupported technologies):
        If the user requests a technology, pattern, or architectural style NOT in the supported stack,
        you MUST:
        1. Name the unsupported item explicitly.
        2. Explain why it is not in the supported stack (scope, architecture, or technology reason).
        3. Offer the closest supported alternative with a clear explanation of trade-offs.
        4. Allow the user to continue — refusal does not end the session.

        EC-4 HANDLING (no compatible solution):
        If after multiple exchanges you cannot propose a VSA-compatible solution for the user's requirements:
        1. Acknowledge the constraint clearly.
        2. Explain why the requirement is incompatible with the supported stack.
        3. Propose the closest alternative that IS supported.
        4. Offer escalation: "If this constraint is non-negotiable, I can help you create an escalation
           request for architect review. Just say 'escalate' to proceed."
        Do NOT generate a Solution.dsl if no VSA-compatible solution exists.

        ---

        YOUR INSTRUCTIONS:
        - Reference the locked Business.dsl throughout the conversation; never contradict its stated constraints or non-goals.
        - Propose solution options in terms of project types and modules from the supported stack above.
        - Explain trade-offs between options in business-friendly language.
        - Ask clarifying questions if requirements are ambiguous.
        - Guide the user toward a Solution.dsl structure (projects + modules + applies_to relationships).
        - When the user is ready to commit, tell them to click "Generate Solution.dsl" to produce the output document.
        - Maintain a professional but conversational tone.
        """;

    private static string BuildPhase3SystemPrompt(string businessDsl, string solutionDsl) => $"""
        You are a software planning assistant helping to decompose a defined solution into a
        structured, machine-readable backlog.

        You have two read-only context documents provided below. Do not allow the user to modify them.

        YOUR INSTRUCTIONS:
        - Decompose the solution into features, user stories, and implementation tasks
        - Enforce vertical slice architecture: each slice delivers end-to-end user value
        - Enforce correct dependency direction: higher layers depend on lower layers, never the reverse
        - Enforce bounded context boundaries: tasks must not span multiple bounded contexts without justification
        - Each task must be: small (completable by one developer in one working day), testable
          (has at least one stated acceptance criterion), and independently implementable
          (no undeclared dependencies on in-progress tasks)
        - Generate tasks referencing specific projects from Solution.dsl
        - Refuse to silently include tasks that violate vertical slice principles — flag them instead
          and explain the violation, then propose a compliant alternative
        - Tasks should be concrete enough for a developer to start immediately

        BUSINESS.DSL (read-only):
        ```yaml
        {businessDsl}
        ```

        SOLUTION.DSL (read-only):
        ```yaml
        {solutionDsl}
        ```
        """;

    private const string BusinessDslGenerationPrompt = """
        Based on our conversation so far, please generate the Business.dsl YAML document.

        Output ONLY the YAML document — no prose, no explanation, no markdown code fence.
        The output must be valid YAML 1.2, UTF-8, with LF line endings.

        Follow this schema exactly:

        version: "1.0"

        project:
          name: <string>        # Required. Plain-language project name.
          summary: <string>     # Required. 1-3 sentence plain-language summary.

        goals:                  # Required. At least one entry.
          - id: <string>        # e.g. "G1"
            statement: <string>

        constraints:            # Required. At least one entry.
          - id: <string>
            statement: <string>

        non_goals:              # Required. At least one entry.
          - id: <string>
            statement: <string>

        open_questions:         # Optional. Items still unresolved.
          - id: <string>
            question: <string>
        """;

    private const string SolutionDslGenerationPrompt = """
        Based on our solution shaping conversation, please generate the Solution.dsl YAML document.

        Output ONLY the YAML document — no prose, no explanation, no markdown code fence.
        The output must be valid YAML 1.2, UTF-8, with LF line endings.

        Use ONLY these exact enum values:
        - project.type: API | Infrastructure | SharedContractsSDK | MauiHybridUI | Worker
        - module.name:  Auth | CQRS | EFCore | Observability | Validation | Caching | Messaging

        Follow this schema exactly:

        version: "1.0"

        solution:
          name: <string>              # Required. Matches project.name from Business.dsl.
          business_dsl_ref: "./business.yaml"

        projects:                     # Required. At least one entry.
          - name: <string>            # e.g. "StaffLeave.Api"
            type: <enum>              # API | Infrastructure | SharedContractsSDK | MauiHybridUI | Worker
            description: <string>    # What this project is responsible for.

        modules:                      # Optional. Omit if no cross-cutting modules apply.
          - name: <enum>              # Auth | CQRS | EFCore | Observability | Validation | Caching | Messaging
            applies_to:              # List of project names this module applies to.
              - <project-name>
        """;

    private const string PlanDslGenerationPrompt = """
        Based on our decomposition conversation, please generate the Plan.dsl YAML document.

        Output ONLY the YAML document — no prose, no explanation, no markdown code fence.
        The output must be valid YAML 1.2, UTF-8, with LF line endings.

        Follow this schema exactly:

        version: "1.0"

        plan:
          name: <string>            # Required. Matches project.name from Business.dsl.
          solution_dsl_ref: "./Solution.dsl"

        features:                   # Required. At least one entry.
          - id: <string>            # e.g. "F1"
            title: <string>
            description: <string>
            bounded_context: <string>
            stories:                # Required. At least one per feature.
              - id: <string>        # e.g. "F1-S1"
                title: <string>
                as_a: <string>
                i_want: <string>
                so_that: <string>
                tasks:              # Required. At least one per story.
                  - id: <string>    # e.g. "F1-S1-T1"
                    title: <string>
                    description: <string>
                    project: <string>
                    priority: <"high"|"normal"|"low">
                    acceptance_criteria:
                      - <string>
                    dependencies: []
                    estimated_size: <"XS"|"S"|"M"|"L">
        """;
}
