using AiDev.Core.Local.Contracts;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiDev.Core.Local.Implementation;

internal sealed class LlmPlanner(IEnumerable<ILlmClient> clients) : ILocalPlanner
{
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

        var prompt = BuildPlanningPrompt(state);
        var maxRetries = state.Budget.MaxRetriesPerError;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            var response = await client.CompleteAsync(prompt, state.ModelProfile.ModelId, ct);
            if (response is Err<string> err) return new Err<RuntimeActionPlan>(err.Error);

            var plan = TryParsePlan(((Ok<string>)response).Value);
            if (plan is not null) return new Ok<RuntimeActionPlan>(plan);
        }

        return new Err<RuntimeActionPlan>(new DomainError(
            "LlmPlanner.ParseFailed",
            $"Failed to parse a valid plan after {maxRetries + 1} attempt(s)."));
    }

    private static string BuildPlanningPrompt(LocalRuntimeState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a local AI development assistant planning the next action.");
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

        sb.AppendLine();
        sb.AppendLine("Available tools:");
        sb.AppendLine("  read_file  — args: path");
        sb.AppendLine("  list_dir   — args: path (optional)");
        sb.AppendLine("  grep       — args: pattern, dir (optional), extension (optional)");
        sb.AppendLine("  glob       — args: pattern, dir (optional)");
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
        sb.AppendLine("If the objective is complete, use an empty toolRequests array.");
        return sb.ToString();
    }

    private static RuntimeActionPlan? TryParsePlan(string response)
    {
        var json = ExtractJson(response);
        if (json is null) return null;

        try
        {
            var dto = JsonSerializer.Deserialize<PlanDto>(json, JsonDefaults.Read);
            if (dto?.Intent is null || dto.ToolRequests is null || dto.ExpectedOutcome is null)
                return null;

            var requests = dto.ToolRequests
                .Select(r => new ToolRequest(
                    r.ToolName ?? string.Empty,
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

    // Extracts the first balanced JSON object from the response, handling preamble text.
    private static string? ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0) return null;

        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0) return text[start..(i + 1)];
            }
        }
        return null;
    }

    private sealed class PlanDto
    {
        [JsonPropertyName("intent")]
        public string? Intent { get; set; }

        [JsonPropertyName("toolRequests")]
        public List<ToolRequestDto>? ToolRequests { get; set; }

        [JsonPropertyName("expectedOutcome")]
        public string? ExpectedOutcome { get; set; }

        [JsonPropertyName("requiresUserInput")]
        public bool RequiresUserInput { get; set; }
    }

    private sealed class ToolRequestDto
    {
        [JsonPropertyName("toolName")]
        public string? ToolName { get; set; }

        [JsonPropertyName("arguments")]
        public Dictionary<string, string>? Arguments { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }
}
