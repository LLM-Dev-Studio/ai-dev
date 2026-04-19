using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AiDev.Features.Planning;
using AiDev.Services;

using Microsoft.Extensions.Logging;

namespace AiDev.Executors;

/// <summary>
/// Anthropic Messages API-backed implementation of <see cref="IPlanningChatService"/>.
///
/// Implements:
/// - Phase 1 (Business Discovery): system prompt with implementation-detail prohibition,
///   EC-6 keyword filter on all assistant responses (AD-02 blocklist).
/// - Phase 2 (Solution Shaping): Solution Architect role constrained to the VSA stack taxonomy
///   (AD-09). Refusal rules for excluded technologies, EC-4 handling.
///   Solution.dsl generation prompt constrained to valid type/module enumerations (AD-10).
/// - Phase 3 (Planning &amp; Decomposition): system prompt with Business.dsl and Solution.dsl
///   injected as read-only context.
/// - Token counting from API response: soft warning at 32,000 input tokens,
///   hard limit at 40,000 input tokens per phase.
/// - Business.dsl, Solution.dsl, and Plan.dsl YAML generation prompts.
/// </summary>
public sealed class AnthropicPlanningChatService(
    IHttpClientFactory httpClientFactory,
    StudioSettingsService settingsService,
    ILogger<AnthropicPlanningChatService> logger) : IPlanningChatService
{
    private const string AnthropicApiUrl = "https://api.anthropic.com/v1/messages";
    private const string ModelId         = "claude-haiku-4-5-20251001"; // cost-effective for planning conversations
    private const int    MaxTokens       = 8192;

    // -------------------------------------------------------------------------
    // Phase 1 — Business Discovery
    // -------------------------------------------------------------------------

    public async Task<PlanningChatResponse> SendPhase1MessageAsync(
        IReadOnlyList<ConversationTurn> history,
        string userMessage,
        CancellationToken ct = default)
    {
        var messages = BuildMessages(history, userMessage);
        var (content, inputTokens, outputTokens) =
            await CallApiAsync(Phase1SystemPrompt, messages, ct).ConfigureAwait(false);

        // EC-6: apply implementation-detail keyword filter to Phase 1 responses.
        var firstMatch  = PlanningKeywordBlocklist.FindFirstMatch(content);
        var wasFiltered = firstMatch != null;

        var tokenStatus = TokenStatus.From(inputTokens);

        if (wasFiltered)
        {
            IReadOnlyList<string> filteredTerms = [firstMatch!];

            logger.LogInformation(
                "[planning-chat] EC-6 filter triggered — terms: {Terms}",
                string.Join(", ", filteredTerms));

            const string blockedMessage =
                "I notice I was about to mention some technical implementation details, which aren't appropriate for this phase. " +
                "Let's keep our focus on the business problem. Could you rephrase your last message in purely business terms? " +
                "For example, instead of naming specific technologies, describe what the system needs to do for the users.";

            return new PlanningChatResponse(
                blockedMessage,
                WasFiltered: true,
                filteredTerms,
                inputTokens,
                outputTokens,
                tokenStatus);
        }

        return new PlanningChatResponse(
            content,
            WasFiltered: false,
            [],
            inputTokens,
            outputTokens,
            tokenStatus);
    }

    public async Task<string> GenerateBusinessDslAsync(
        IReadOnlyList<ConversationTurn> history,
        CancellationToken ct = default)
    {
        var messages = BuildMessages(history, BusinessDslGenerationPrompt);
        var (content, _, _) = await CallApiAsync(Phase1SystemPrompt, messages, ct).ConfigureAwait(false);
        return content;
    }

    // -------------------------------------------------------------------------
    // Phase 2 — Solution Shaping
    // -------------------------------------------------------------------------

    public async Task<PlanningChatResponse> SendPhase2MessageAsync(
        IReadOnlyList<ConversationTurn> history,
        string businessDsl,
        string userMessage,
        CancellationToken ct = default)
    {
        var systemPrompt = BuildPhase2SystemPrompt(businessDsl);
        var messages     = BuildMessages(history, userMessage);
        var (content, inputTokens, outputTokens) =
            await CallApiAsync(systemPrompt, messages, ct).ConfigureAwait(false);

        var tokenStatus = TokenStatus.From(inputTokens);

        return new PlanningChatResponse(
            content,
            WasFiltered: false,
            [],
            inputTokens,
            outputTokens,
            tokenStatus);
    }

    public async Task<string> GenerateSolutionDslAsync(
        IReadOnlyList<ConversationTurn> history,
        string businessDsl,
        CancellationToken ct = default)
    {
        var systemPrompt = BuildPhase2SystemPrompt(businessDsl);
        var messages     = BuildMessages(history, SolutionDslGenerationPrompt);
        var (content, _, _) = await CallApiAsync(systemPrompt, messages, ct).ConfigureAwait(false);
        return content;
    }

    // -------------------------------------------------------------------------
    // Phase 3 — Planning & Decomposition
    // -------------------------------------------------------------------------

    public async Task<PlanningChatResponse> SendPhase3MessageAsync(
        IReadOnlyList<ConversationTurn> history,
        string businessDsl,
        string solutionDsl,
        string userMessage,
        CancellationToken ct = default)
    {
        var systemPrompt = BuildPhase3SystemPrompt(businessDsl, solutionDsl);
        var messages     = BuildMessages(history, userMessage);
        var (content, inputTokens, outputTokens) =
            await CallApiAsync(systemPrompt, messages, ct).ConfigureAwait(false);

        var tokenStatus = TokenStatus.From(inputTokens);

        return new PlanningChatResponse(
            content,
            WasFiltered: false,
            [],
            inputTokens,
            outputTokens,
            tokenStatus);
    }

    public async Task<string> GeneratePlanDslAsync(
        IReadOnlyList<ConversationTurn> history,
        string businessDsl,
        string solutionDsl,
        CancellationToken ct = default)
    {
        var systemPrompt = BuildPhase3SystemPrompt(businessDsl, solutionDsl);
        var messages     = BuildMessages(history, PlanDslGenerationPrompt);
        var (content, _, _) = await CallApiAsync(systemPrompt, messages, ct).ConfigureAwait(false);
        return content;
    }

    // -------------------------------------------------------------------------
    // HTTP helper
    // -------------------------------------------------------------------------

    private async Task<(string Content, int InputTokens, int OutputTokens)> CallApiAsync(
        string systemPrompt, JsonArray messages, CancellationToken ct)
    {
        var apiKey = settingsService.GetSettings().AnthropicApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("AnthropicApiKey not configured in studio settings.");

        var requestBody = new JsonObject
        {
            ["model"]      = ModelId,
            ["max_tokens"] = MaxTokens,
            ["system"]     = systemPrompt,
            ["messages"]   = messages,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, AnthropicApiUrl)
        {
            Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var http = httpClientFactory.CreateClient("anthropic");
        HttpResponseMessage response;

        try
        {
            response = await http.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[planning-chat] Failed to call Anthropic API");
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            logger.LogError("[planning-chat] Anthropic returned HTTP {Status}: {Body}",
                (int)response.StatusCode, body);
            throw new HttpRequestException(
                $"Anthropic API returned HTTP {(int)response.StatusCode}: {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = doc.RootElement;

        var content = root
            .GetProperty("content")
            .EnumerateArray()
            .Where(b => b.TryGetProperty("type", out var t) && t.GetString() == "text")
            .Select(b => b.GetProperty("text").GetString() ?? string.Empty)
            .FirstOrDefault() ?? string.Empty;

        var inputTokens  = 0;
        var outputTokens = 0;
        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("input_tokens",  out var it)) inputTokens  = it.GetInt32();
            if (usage.TryGetProperty("output_tokens", out var ot)) outputTokens = ot.GetInt32();
        }

        logger.LogDebug("[planning-chat] tokens: {Input} in / {Output} out", inputTokens, outputTokens);
        return (content, inputTokens, outputTokens);
    }

    // -------------------------------------------------------------------------
    // Message building
    // -------------------------------------------------------------------------

    private static JsonArray BuildMessages(IReadOnlyList<ConversationTurn> history, string newUserMessage)
    {
        var array = new JsonArray();

        // Add history turns (interleaved user/assistant as Anthropic requires).
        foreach (var turn in history)
        {
            array.Add(new JsonObject
            {
                ["role"]    = turn.Role == ConversationRole.User ? "user" : "assistant",
                ["content"] = turn.Content,
            });
        }

        // Append the new user message.
        array.Add(new JsonObject
        {
            ["role"]    = "user",
            ["content"] = newUserMessage,
        });

        return array;
    }

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

