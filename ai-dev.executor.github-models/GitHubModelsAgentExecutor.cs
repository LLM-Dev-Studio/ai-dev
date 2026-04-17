using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

using AiDev.Services;

using Microsoft.Extensions.Logging;

namespace AiDev.Executors;

/// <summary>
/// Runs an agent session via the GitHub Models API — an OpenAI-compatible endpoint
/// at https://models.github.ai/inference/chat/completions authenticated with a
/// GitHub personal access token (requires the models:read scope).
///
/// Available models include GPT-4o, o1, DeepSeek, Llama, Phi, and others.
/// The health check fetches the catalog at https://models.github.ai/catalog/models
/// so the UI model dropdown auto-populates from the executor's health check Details.
///
/// When the "mcp-workspace" skill is enabled, workspace tool schemas are included in
/// the request (OpenAI function-calling format, same as the Ollama executor). The
/// executor handles tool_calls returned by the model, executes the corresponding
/// workspace operations, appends results to the message history, and re-invokes the
/// model — continuing until the model produces a final response with no tool calls.
///
/// Rate limits (HTTP 429) are detected from the response status code and surfaced as
/// <see cref="ExecutorResult.IsRateLimited"/> = true, suppressing re-launches.
/// </summary>
public sealed class GitHubModelsAgentExecutor(
    IHttpClientFactory httpClientFactory,
    StudioSettingsService settingsService,
    ILogger<GitHubModelsAgentExecutor> logger) : IAgentExecutor
{
    public string Name        => "github-models";
    public string DisplayName => "GitHub Models";

    public IReadOnlyList<ExecutorSkill> AvailableSkills { get; } = GitHubModelsSkills.All;

    /// <summary>
    /// GitHub Models are fetched from the catalog — none are known statically.
    /// The health check discovers them dynamically from /catalog/models.
    /// </summary>
    public IReadOnlyList<ModelDescriptor> KnownModels => [];

    private const string BaseUrl          = "https://models.github.ai";
    private const string ChatEndpoint     = $"{BaseUrl}/inference/chat/completions";
    private const string CatalogEndpoint  = $"{BaseUrl}/catalog/models";
    private const int    MaxToolIterations = 30;
    private const int    DefaultMaxTokens  = 4096;

    // -------------------------------------------------------------------------
    // Health check
    // -------------------------------------------------------------------------

    public async Task<ExecutorHealthResult> CheckHealthAsync(CancellationToken ct = default)
    {
        var token = settingsService.GetSettings().GitHubToken;
        if (string.IsNullOrWhiteSpace(token))
            return new ExecutorHealthResult(false, "GitHubToken not configured in studio settings.");

        try
        {
            var http = httpClientFactory.CreateClient("github-models-health");
            using var request = new HttpRequestMessage(HttpMethod.Get, CatalogEndpoint);
            AddHeaders(request, token);

            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return new ExecutorHealthResult(false, "GitHub token rejected (401 Unauthorized).");

            if (!response.IsSuccessStatusCode)
                return new ExecutorHealthResult(false, $"GitHub Models returned HTTP {(int)response.StatusCode}");

            List<ModelDescriptor> discovered = [];
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

                // Catalog returns a top-level array: [ { "id": "openai/gpt-4o", "name": "GPT-4o", ... }, ... ]
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    discovered = doc.RootElement.EnumerateArray()
                        .Select(m =>
                        {
                            var id   = m.TryGetProperty("id",   out var idProp)   ? idProp.GetString()   ?? string.Empty : string.Empty;
                            var name = m.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? id            : id;
                            return (id, name);
                        })
                        .Where(t => t.id.Length > 0)
                        .OrderBy(t => t.id, StringComparer.OrdinalIgnoreCase)
                        .Select(t => new ModelDescriptor(t.id, t.name, "github-models",
                            ModelCapabilities.Streaming | ModelCapabilities.ToolCalling))
                        .ToList();
                }
            }
            catch { /* model list is best-effort */ }

            var message = discovered.Count > 0
                ? $"{discovered.Count} model(s) available"
                : "Connected";

            return new ExecutorHealthResult(true, message, discovered);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[github-models-health] Probe failed: {Message}", ex.Message);
            return new ExecutorHealthResult(false, $"Connection failed: {ex.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // Run
    // -------------------------------------------------------------------------

    public async Task<ExecutorResult> RunAsync(ExecutorContext context, ChannelWriter<string> output)
    {
        var token = settingsService.GetSettings().GitHubToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            const string msg = "GitHubToken not configured in studio settings. Aborting.";
            logger.LogError("[github-models] {Message}", msg);
            output.TryWrite($"[{DateTime.UtcNow:o}] [error] {msg}");
            return new ExecutorResult(1, ErrorMessage: msg);
        }

        var systemPrompt  = BuildSystemPrompt(context.WorkingDir);
        var enableTools   = GitHubModelsSkills.AreWorkspaceToolsEnabled(context.EnabledSkills);
        var workspaceRoot = enableTools ? DeriveWorkspaceRoot(context.WorkingDir) : null;

        logger.LogInformation(
            "[github-models] Starting session — model={Model} tools={Tools}",
            context.ModelId, enableTools ? "enabled" : "disabled");

        output.TryWrite($"[{DateTime.UtcNow:o}] [github-models] model={context.ModelId}");

        if (enableTools)
            output.TryWrite($"[{DateTime.UtcNow:o}] [github-models] workspace tools enabled — root={workspaceRoot}");

        // Build mutable message history for the tool execution loop.
        var messages = new List<JsonNode>
        {
            new JsonObject { ["role"] = "system", ["content"] = systemPrompt },
            new JsonObject { ["role"] = "user",   ["content"] = context.Prompt },
        };

        var http      = httpClientFactory.CreateClient("github-models");
        int iteration = 0;
        TokenUsage? totalUsage = null;

        while (iteration++ < MaxToolIterations)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            var requestBody = BuildRequest(context.ModelId, messages, enableTools, context.ThinkingLevel);

            HttpResponseMessage response;
            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ChatEndpoint)
                {
                    Content = new StringContent(requestBody, Encoding.UTF8, "application/json"),
                };
                AddHeaders(httpRequest, token);

                response = await http.SendAsync(
                    httpRequest, HttpCompletionOption.ResponseHeadersRead, context.CancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var msg = $"Failed to connect to GitHub Models API: {ex.Message}";
                logger.LogError(ex, "[github-models] {Message}", msg);
                output.TryWrite($"[{DateTime.UtcNow:o}] [error] {msg}");
                return new ExecutorResult(1, ErrorMessage: msg);
            }

            // Scope disposal to this loop iteration (catch above always returns,
            // so response is always non-null here).
            using var _ = response;

            // Rate limit — suppress re-launch.
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var body       = await response.Content.ReadAsStringAsync(context.CancellationToken).ConfigureAwait(false);
                var retryAfter = response.Headers.RetryAfter?.Delta;
                var hint       = retryAfter.HasValue ? $" retry-after={retryAfter.Value.TotalSeconds:F0}s" : "";
                var msg        = $"GitHub Models rate limit reached.{hint}";
                logger.LogWarning("[github-models] {Message} Body={Body}", msg, body);
                output.TryWrite($"[{DateTime.UtcNow:o}] [rate-limit] {msg}");
                return new ExecutorResult(1, IsRateLimited: true, ErrorMessage: msg);
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(context.CancellationToken).ConfigureAwait(false);
                var msg  = $"GitHub Models returned HTTP {(int)response.StatusCode}: {body}";
                logger.LogError("[github-models] {Message}", msg);
                output.TryWrite($"[{DateTime.UtcNow:o}] [error] {msg}");
                return new ExecutorResult(1, ErrorMessage: msg);
            }

            logger.LogInformation("[github-models] Streaming response — iteration {N}", iteration);

            // Stream and parse OpenAI SSE response.
            var (finishReason, textContent, toolCalls, sessionUsage) =
                await StreamSseAsync(response, output, context.CancellationToken).ConfigureAwait(false);

            // Accumulate usage across all tool-call iterations.
            if (sessionUsage != null)
                totalUsage = totalUsage == null ? sessionUsage : totalUsage + sessionUsage;

            logger.LogInformation("[github-models] finish_reason={Reason} iteration={N}", finishReason, iteration);

            // Final response: no tool calls.
            if (toolCalls.Count == 0)
            {
                logger.LogInformation(
                    "[github-models] Session complete — {Chars} chars | iterations: {N}",
                    textContent.Length, iteration);
                return new ExecutorResult(0, Usage: totalUsage);
            }

            // Tool calls requested — execute and loop.
            logger.LogInformation(
                "[github-models] Tool calls requested — count={Count} iteration={N}", toolCalls.Count, iteration);
            output.TryWrite($"[{DateTime.UtcNow:o}] [github-models] tool calls: {toolCalls.Count} (iteration {iteration})");

            // Append assistant message with tool_calls.
            var toolCallsArray = new JsonArray();
            foreach (var tc in toolCalls)
            {
                toolCallsArray.Add(new JsonObject
                {
                    ["id"]   = tc.Id,
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"]      = tc.Name,
                        ["arguments"] = tc.Arguments.ToString(),
                    },
                });
            }

            var assistantMsg = new JsonObject
            {
                ["role"]       = "assistant",
                ["content"]    = textContent.Length > 0 ? (JsonNode?)textContent : null,
                ["tool_calls"] = toolCallsArray,
            };
            messages.Add(assistantMsg);

            // Execute each tool and append tool result messages.
            foreach (var tc in toolCalls)
            {
                var argsJson    = tc.Arguments.ToString();
                var argsPreview = argsJson.Length > 80 ? argsJson[..80] + "…" : argsJson;
                output.TryWrite($"[{DateTime.UtcNow:o}] [tool:call] {tc.Name}({argsPreview})");
                logger.LogInformation("[github-models] Executing tool — {Tool}({Args})", tc.Name, argsPreview);

                JsonElement argsElement;
                try   { argsElement = JsonDocument.Parse(argsJson).RootElement; }
                catch { argsElement = JsonDocument.Parse("{}").RootElement; }

                var result = workspaceRoot != null
                    ? WorkspaceTools.Execute(workspaceRoot, tc.Name, argsElement)
                    : "[error] mcp-workspace not enabled";

                var preview = result.Length > 120
                    ? result[..120].Replace('\n', ' ') + "…"
                    : result.Replace('\n', ' ');
                output.TryWrite($"[{DateTime.UtcNow:o}] [tool:result] {preview}");
                logger.LogInformation("[github-models] Tool result — {Chars} chars", result.Length);

                messages.Add(new JsonObject
                {
                    ["role"]         = "tool",
                    ["tool_call_id"] = tc.Id,
                    ["content"]      = result,
                });
            }
        }

        var limitMsg = $"Exceeded maximum tool-call iterations ({MaxToolIterations}). Aborting session.";
        logger.LogError("[github-models] {Message}", limitMsg);
        output.TryWrite($"[{DateTime.UtcNow:o}] [error] {limitMsg}");
        return new ExecutorResult(1, ErrorMessage: limitMsg);
    }

    // -------------------------------------------------------------------------
    // SSE streaming
    // -------------------------------------------------------------------------

    /// <summary>
    /// Streams the OpenAI-format SSE response, emitting text tokens as they arrive.
    /// </summary>
    /// <returns>finish_reason string, accumulated text, and any tool calls.</returns>
    private async Task<(string FinishReason, string TextContent, List<ToolCall> ToolCalls, TokenUsage? Usage)>
        StreamSseAsync(HttpResponseMessage response, ChannelWriter<string> output, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader       = new StreamReader(stream, Encoding.UTF8);

        var textBuilder  = new StringBuilder();
        var finishReason = string.Empty;
        var toolCalls    = new Dictionary<int, ToolCall>(); // index → accumulator
        TokenUsage? capturedUsage = null;

        string? line;
        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) != null)
        {
            if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;

            var json = line["data: ".Length..];
            if (json == "[DONE]") break;

            JsonElement evt;
            try   { evt = JsonDocument.Parse(json).RootElement; }
            catch { continue; }

            if (!evt.TryGetProperty("choices", out var choices)) continue;

            foreach (var choice in choices.EnumerateArray())
            {
                // finish_reason on this choice
                if (choice.TryGetProperty("finish_reason", out var fr) && fr.ValueKind != JsonValueKind.Null)
                    finishReason = fr.GetString() ?? string.Empty;

                if (!choice.TryGetProperty("delta", out var delta)) continue;

                // Text token
                if (delta.TryGetProperty("content", out var contentProp)
                    && contentProp.ValueKind == JsonValueKind.String)
                {
                    var text = contentProp.GetString() ?? "";
                    textBuilder.Append(text);
                    if (text.Length > 0)
                        output.TryWrite($"[{DateTime.UtcNow:o}] {text}");
                }

                // Tool call deltas (streamed incrementally by index)
                if (!delta.TryGetProperty("tool_calls", out var tcArray)) continue;

                foreach (var tcDelta in tcArray.EnumerateArray())
                {
                    var idx = tcDelta.TryGetProperty("index", out var idxProp) ? idxProp.GetInt32() : 0;

                    if (!toolCalls.TryGetValue(idx, out var tc))
                    {
                        var id   = tcDelta.TryGetProperty("id",   out var idProp)   ? idProp.GetString()   ?? "" : "";
                        tc = new ToolCall(id);
                        toolCalls[idx] = tc;
                    }

                    if (!tcDelta.TryGetProperty("function", out var fn)) continue;

                    if (fn.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                        tc.Name = nameProp.GetString() ?? tc.Name;

                    if (fn.TryGetProperty("arguments", out var argsProp) && argsProp.ValueKind == JsonValueKind.String)
                        tc.Arguments.Append(argsProp.GetString());
                }
            }

            // Usage on the final chunk (non-streaming field sometimes included)
            if (evt.TryGetProperty("usage", out var usage) && usage.ValueKind != JsonValueKind.Null)
            {
                var promptTokens = usage.TryGetProperty("prompt_tokens",     out var pt)  ? pt.GetInt32()  : 0;
                var outputTokens = usage.TryGetProperty("completion_tokens", out var ct2) ? ct2.GetInt32() : 0;
                if (promptTokens > 0 || outputTokens > 0)
                {
                    capturedUsage = new TokenUsage(promptTokens, outputTokens);
                    output.TryWrite(
                        $"[{DateTime.UtcNow:o}] [github-models] tokens: {promptTokens} prompt / {outputTokens} generated");
                }
            }
        }

        var completedCalls = toolCalls
            .OrderBy(kv => kv.Key)
            .Select(kv => kv.Value)
            .Where(tc => tc.Name.Length > 0)
            .ToList();

        return (finishReason, textBuilder.ToString(), completedCalls, capturedUsage);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static string BuildRequest(string modelId, List<JsonNode> messages, bool includeTools,
        ThinkingLevel thinkingLevel = ThinkingLevel.Off)
    {
        var obj = new JsonObject
        {
            ["model"]      = modelId,
            ["max_tokens"] = DefaultMaxTokens,
            ["messages"]   = new JsonArray(messages.Select(m => m.DeepClone()).ToArray()),
            ["stream"]     = true,
            ["stream_options"] = new JsonObject { ["include_usage"] = true },
        };

        if (thinkingLevel != ThinkingLevel.Off)
        {
            var effort = thinkingLevel.ToReasoningEffort();
            if (effort != null) obj["reasoning_effort"] = effort;
        }

        if (includeTools)
            obj["tools"] = OllamaToolSchemas.GetToolsArray();

        return obj.ToJsonString();
    }

    private static void AddHeaders(HttpRequestMessage request, string token)
    {
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Accept", "application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    private static string BuildSystemPrompt(string workingDir)
    {
        var claudeMd = Path.Combine(workingDir, "CLAUDE.md");
        if (!File.Exists(claudeMd)) return "You are a helpful AI agent.";
        try { return File.ReadAllText(claudeMd, Encoding.UTF8); }
        catch { return "You are a helpful AI agent."; }
    }

    private static string DeriveWorkspaceRoot(string workingDir) =>
        Path.GetFullPath(Path.Combine(workingDir, "../.."));

    // -------------------------------------------------------------------------
    // Tool call accumulator
    // -------------------------------------------------------------------------

    private sealed class ToolCall(string id)
    {
        public string        Id        { get; set; }    = id;
        public string        Name      { get; set; }   = string.Empty;
        public StringBuilder Arguments { get; }        = new();
    }
}
