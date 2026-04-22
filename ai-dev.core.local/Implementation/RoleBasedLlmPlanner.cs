using AiDev.Core.Local.Contracts;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiDev.Core.Local.Implementation;

internal sealed class RoleBasedLlmPlanner(IEnumerable<ILlmClient> clients) : ILocalPlanner
{
    private static readonly IReadOnlyDictionary<LocalAgentRole, string[]> ToolWhitelists =
        new Dictionary<LocalAgentRole, string[]>
        {
            [LocalAgentRole.Planner]    = ["list_dir", "glob"],
            [LocalAgentRole.Researcher] = ["read_file", "grep", "glob"],
            [LocalAgentRole.Coder]      = ["read_file", "grep", "glob", "list_dir"],
        };

    public async Task<Result<RuntimeActionPlan>> PlanNextAsync(
        LocalRuntimeState state,
        CancellationToken ct = default)
    {
        var provider = state.ModelProfile.Provider ?? string.Empty;
        var client = clients.FirstOrDefault(c =>
            c.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase));

        if (client is null)
            return new Err<RuntimeActionPlan>(new DomainError(
                "LlmPlanner.NoClient",
                $"No LLM client registered for provider '{provider}'."));

        var role = SelectRole(state);
        var prompt = BuildPrompt(role, state);
        var maxRetries = state.Budget.MaxRetriesPerError;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            var response = await client.CompleteAsync(prompt, state.ModelProfile.ModelId, ct);
            if (response is Err<string> err) return new Err<RuntimeActionPlan>(err.Error);

            var plan = TryParse(((Ok<string>)response).Value, role);
            if (plan is not null) return new Ok<RuntimeActionPlan>(plan);
        }

        return new Err<RuntimeActionPlan>(new DomainError(
            "LlmPlanner.ParseFailed",
            $"Failed to parse a valid plan after {maxRetries + 1} attempt(s)."));
    }

    // Role selection: first pass → Planner; structural-only observations → Researcher; file reads present → Coder.
    internal static LocalAgentRole SelectRole(LocalRuntimeState state)
    {
        if (state.Iteration == 0 || state.Transcript.Observations.Count == 0)
            return LocalAgentRole.Planner;

        var recentSources = state.Transcript.Observations
            .TakeLast(3)
            .Select(o => o.Source)
            .ToList();

        var hasDeepRead = recentSources.Any(s =>
            s.StartsWith("read_file", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("grep", StringComparison.OrdinalIgnoreCase));

        return hasDeepRead ? LocalAgentRole.Coder : LocalAgentRole.Researcher;
    }

    private static string BuildPrompt(LocalAgentRole role, LocalRuntimeState state)
    {
        var sb = new StringBuilder();

        // Role-specific persona header
        sb.AppendLine(role switch
        {
            LocalAgentRole.Planner =>
                "You are a planning assistant mapping the scope of a software objective.",
            LocalAgentRole.Researcher =>
                "You are a research assistant investigating a software codebase for evidence.",
            LocalAgentRole.Coder =>
                "You are a synthesis assistant drawing conclusions from gathered codebase evidence.",
            _ => "You are a local AI development assistant."
        });

        sb.AppendLine();
        sb.Append("Objective: ").AppendLine(state.Objective.Goal);
        if (!string.IsNullOrWhiteSpace(state.Objective.SuccessCriteria))
            sb.Append("Success criteria: ").AppendLine(state.Objective.SuccessCriteria);

        sb.AppendLine();
        sb.AppendLine("Recent observations:");
        foreach (var obs in state.Transcript.Observations.TakeLast(5))
            sb.Append("  - [").Append(obs.Source).Append("] ").AppendLine(obs.Summary);
        if (state.Transcript.Observations.Count == 0)
            sb.AppendLine("  (none yet)");

        var tools = ToolWhitelists[role];
        sb.AppendLine();
        sb.Append("Available tools for your role (").Append(role).AppendLine("):");
        foreach (var t in tools)
            sb.Append("  ").AppendLine(t switch
            {
                "read_file" => "read_file  — args: path",
                "list_dir"  => "list_dir   — args: path (optional)",
                "grep"      => "grep       — args: pattern, dir (optional), extension (optional)",
                "glob"      => "glob       — args: pattern, dir (optional)",
                _           => t
            });

        sb.AppendLine();
        sb.AppendLine("Respond with ONLY valid JSON:");
        sb.AppendLine("{");
        sb.AppendLine("  \"intent\": \"Brief description\",");
        sb.AppendLine("  \"toolRequests\": [");
        sb.AppendLine("    { \"toolName\": \"...\", \"arguments\": { \"key\": \"value\" }, \"reason\": \"why\" }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"expectedOutcome\": \"What you expect to find or accomplish\",");
        sb.AppendLine("  \"requiresUserInput\": false");
        sb.AppendLine("}");
        sb.AppendLine("Only use tools from the list above. Empty toolRequests signals objective complete.");
        return sb.ToString();
    }

    private static RuntimeActionPlan? TryParse(string response, LocalAgentRole role)
    {
        var json = ExtractJson(response);
        if (json is null) return null;

        try
        {
            var dto = JsonSerializer.Deserialize<PlanDto>(json, JsonDefaults.Read);
            if (dto?.Intent is null || dto.ToolRequests is null || dto.ExpectedOutcome is null)
                return null;

            var allowed = ToolWhitelists[role];
            var requests = dto.ToolRequests
                .Where(r => r.ToolName is not null && allowed.Contains(r.ToolName, StringComparer.OrdinalIgnoreCase))
                .Select(r => new ToolRequest(
                    r.ToolName!,
                    (IReadOnlyDictionary<string, string>)(r.Arguments ?? new Dictionary<string, string>()),
                    r.Reason ?? string.Empty))
                .ToList();

            return new RuntimeActionPlan(
                Intent: dto.Intent,
                ToolRequests: requests,
                ExpectedOutcome: dto.ExpectedOutcome,
                RequiresUserInput: dto.RequiresUserInput);
        }
        catch { return null; }
    }

    private static string? ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0) return null;
        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}') { depth--; if (depth == 0) return text[start..(i + 1)]; }
        }
        return null;
    }

    private sealed class PlanDto
    {
        [JsonPropertyName("intent")] public string? Intent { get; set; }
        [JsonPropertyName("toolRequests")] public List<ToolRequestDto>? ToolRequests { get; set; }
        [JsonPropertyName("expectedOutcome")] public string? ExpectedOutcome { get; set; }
        [JsonPropertyName("requiresUserInput")] public bool RequiresUserInput { get; set; }
    }

    private sealed class ToolRequestDto
    {
        [JsonPropertyName("toolName")] public string? ToolName { get; set; }
        [JsonPropertyName("arguments")] public Dictionary<string, string>? Arguments { get; set; }
        [JsonPropertyName("reason")] public string? Reason { get; set; }
    }
}
