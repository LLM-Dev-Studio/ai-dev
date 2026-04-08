using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using AiDev.Services;
using AiDev.Mcp;
using AiDev.Mcp.Tools;
using Microsoft.Extensions.Logging;

namespace AiDev.Executors;

/// <summary>
/// Runs an agent session via the Anthropic Messages API (/v1/messages) with SSE streaming.
///
/// Reads CLAUDE.md from the agent directory as the system prompt, then streams the
/// model's response token-by-token to the output channel as text arrives.
///
/// When the "mcp-workspace" skill is enabled, workspace tool schemas are included in
/// the request. The executor handles tool_use blocks returned by the model, executes
/// the corresponding workspace operations via WorkspaceTools (shared with the Ollama
/// executor), appends results to the message history, and re-invokes the model —
/// continuing until the model produces a final response with stop_reason "end_turn".
///
/// Rate limits (HTTP 429) are detected from the response status code and surfaced as
/// <see cref="ExecutorResult.IsRateLimited"/> = true, suppressing re-launches.
/// </summary>
public sealed class AnthropicAgentExecutor(
    IHttpClientFactory httpClientFactory,
    StudioSettingsService settingsService,
    ILogger<AnthropicAgentExecutor> logger) : IAgentExecutor
{
    public string Name        => "anthropic";
    public string DisplayName => "Anthropic (API)";

    public IReadOnlyList<ExecutorSkill> AvailableSkills { get; } = AnthropicSkills.All;

    private const int MaxToolIterations = 30;
    private const int DefaultMaxTokens  = 8096;

    // -------------------------------------------------------------------------
    // Health check
    // -------------------------------------------------------------------------

    public async Task<ExecutorHealthResult> CheckHealthAsync(CancellationToken ct = default)
    {
        var apiKey = settingsService.GetSettings().AnthropicApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return new ExecutorHealthResult(false, "AnthropicApiKey not configured in studio settings.");

        // Anthropic has no ping endpoint — validate by listing models (lightweight GET).
        try
        {
            var http    = httpClientFactory.CreateClient("anthropic-health");
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/models");
            AddHeaders(request, apiKey);

            var response = await http.SendAsync(request, ct).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return new ExecutorHealthResult(false, "API key rejected (401 Unauthorized).");

            if (!response.IsSuccessStatusCode)
                return new ExecutorHealthResult(false, $"Anthropic returned HTTP {(int)response.StatusCode}");

            List<string> models = [];
            try
            {
                await using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                var doc  = await JsonDocument.ParseAsync(contentStream, cancellationToken: ct).ConfigureAwait(false);
                models   = doc?.RootElement.GetProperty("data")
                    .EnumerateArray()
                    .Select(m => m.GetProperty("id").GetString() ?? string.Empty)
                    .Where(id => id.Length > 0)
                    .ToList() ?? [];
            }
            catch { /* model list is best-effort */ }

            var message = models.Count > 0
                ? $"{models.Count} model(s) available"
                : "Connected";

            return new ExecutorHealthResult(true, message, models);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[anthropic-health] Probe failed: {Message}", ex.Message);
            return new ExecutorHealthResult(false, $"Connection failed: {ex.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // Run
    // -------------------------------------------------------------------------

    public async Task<ExecutorResult> RunAsync(ExecutorContext context, ChannelWriter<string> output)
    {
        var apiKey = settingsService.GetSettings().AnthropicApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            const string msg = "AnthropicApiKey not configured in studio settings. Aborting.";
            logger.LogError("[anthropic] {Message}", msg);
            output.TryWrite($"[{DateTime.UtcNow:o}] [error] {msg}");
            return new ExecutorResult(1, ErrorMessage: msg);
        }

        var systemPrompt  = BuildSystemPrompt(context.WorkingDir);
        var enableTools   = context.EnabledSkills.Contains("mcp-workspace");
        var workspaceRoot = enableTools ? DeriveWorkspaceRoot(context.WorkingDir) : null;

        logger.LogInformation(
            "[anthropic] Starting session — model={Model} tools={Tools}",
            context.ModelId, enableTools ? "enabled" : "disabled");

        output.TryWrite($"[{DateTime.UtcNow:o}] [anthropic] model={context.ModelId}");

        if (enableTools)
            output.TryWrite($"[{DateTime.UtcNow:o}] [anthropic] workspace tools enabled — root={workspaceRoot}");

        // Build mutable message history for the tool execution loop.
        var messages = new List<JsonNode>
        {
            new JsonObject { ["role"] = "user", ["content"] = context.Prompt },
        };

        var http      = httpClientFactory.CreateClient("anthropic");
        int iteration = 0;

        while (iteration++ < MaxToolIterations)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            var requestBody = BuildRequest(context.ModelId, systemPrompt, messages, enableTools);

            HttpResponseMessage response;
            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
                {
                    Content = new StringContent(requestBody, Encoding.UTF8, "application/json"),
                };
                AddHeaders(httpRequest, apiKey);

                response = await http.SendAsync(
                    httpRequest, HttpCompletionOption.ResponseHeadersRead, context.CancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var msg = $"Failed to connect to Anthropic API: {ex.Message}";
                logger.LogError(ex, "[anthropic] {Message}", msg);
                output.TryWrite($"[{DateTime.UtcNow:o}] [error] {msg}");
                return new ExecutorResult(1, ErrorMessage: msg);
            }

            // Rate limit — suppress re-launch for 30 minutes.
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var body   = await response.Content.ReadAsStringAsync(context.CancellationToken).ConfigureAwait(false);
                var retryAfter = response.Headers.RetryAfter?.Delta;
                var hint   = retryAfter.HasValue ? $" retry-after={retryAfter.Value.TotalSeconds:F0}s" : "";
                var msg    = $"Anthropic rate limit reached.{hint}";
                logger.LogWarning("[anthropic] {Message} Body={Body}", msg, body);
                output.TryWrite($"[{DateTime.UtcNow:o}] [rate-limit] {msg}");
                return new ExecutorResult(1, IsRateLimited: true, ErrorMessage: msg);
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(context.CancellationToken).ConfigureAwait(false);
                var msg  = $"Anthropic returned HTTP {(int)response.StatusCode}: {body}";
                logger.LogError("[anthropic] {Message}", msg);
                output.TryWrite($"[{DateTime.UtcNow:o}] [error] {msg}");
                return new ExecutorResult(1, ErrorMessage: msg);
            }

            logger.LogInformation("[anthropic] Streaming response — iteration {N}", iteration);

            // --- Stream and parse SSE ---
            var (stopReason, textContent, toolUses) =
                await StreamSseAsync(response, output, context.CancellationToken).ConfigureAwait(false);

            // --- End turn: final response ---
            if (stopReason == "end_turn" || toolUses.Count == 0)
            {
                logger.LogInformation(
                    "[anthropic] Session complete — {Chars} chars | iterations: {N}",
                    textContent.Length, iteration);
                return new ExecutorResult(0);
            }

            // --- Tool use: execute calls and loop ---
            logger.LogInformation(
                "[anthropic] Tool calls requested — count={Count} iteration={N}", toolUses.Count, iteration);
            output.TryWrite($"[{DateTime.UtcNow:o}] [anthropic] tool calls: {toolUses.Count} (iteration {iteration})");

            // Append assistant message (content array with text + tool_use blocks).
            var assistantContent = new JsonArray();
            if (textContent.Length > 0)
                assistantContent.Add(new JsonObject { ["type"] = "text", ["text"] = textContent });

            foreach (var use in toolUses)
            {
                JsonNode? inputNode;
                try   { inputNode = JsonNode.Parse(use.InputJson.ToString()); }
                catch { inputNode = new JsonObject(); }

                assistantContent.Add(new JsonObject
                {
                    ["type"]  = "tool_use",
                    ["id"]    = use.Id,
                    ["name"]  = use.Name,
                    ["input"] = inputNode,
                });
            }
            messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = assistantContent });

            // Execute each tool and collect results.
            var toolResults = new JsonArray();
            foreach (var use in toolUses)
            {
                var inputJson = use.InputJson.ToString();
                var argsPreview = inputJson.Length > 80 ? inputJson[..80] + "…" : inputJson;
                output.TryWrite($"[{DateTime.UtcNow:o}] [tool:call] {use.Name}({argsPreview})");
                logger.LogInformation("[anthropic] Executing tool — {Tool}({Args})", use.Name, argsPreview);

                // Parse input JSON to JsonElement for WorkspaceTools compatibility.
                JsonElement argsElement;
                try   { argsElement = JsonDocument.Parse(inputJson).RootElement; }
                catch { argsElement = JsonDocument.Parse("{}").RootElement; }

                var result = workspaceRoot != null
                    ? WorkspaceTools.Execute(workspaceRoot, use.Name, argsElement)
                    : $"[error] mcp-workspace not enabled";

                var preview = result.Length > 120
                    ? result[..120].Replace('\n', ' ') + "…"
                    : result.Replace('\n', ' ');
                output.TryWrite($"[{DateTime.UtcNow:o}] [tool:result] {preview}");
                logger.LogInformation("[anthropic] Tool result — {Chars} chars", result.Length);

                toolResults.Add(new JsonObject
                {
                    ["type"]        = "tool_result",
                    ["tool_use_id"] = use.Id,
                    ["content"]     = result,
                });
            }

            // Append tool results as a user message (Anthropic's required format).
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = toolResults });
        }

        var limitMsg = $"Exceeded maximum tool-call iterations ({MaxToolIterations}). Aborting session.";
        logger.LogError("[anthropic] {Message}", limitMsg);
        output.TryWrite($"[{DateTime.UtcNow:o}] [error] {limitMsg}");
        return new ExecutorResult(1, ErrorMessage: limitMsg);
    }

    // -------------------------------------------------------------------------
    // SSE streaming
    // -------------------------------------------------------------------------

    /// <summary>Streams the Anthropic SSE response, emitting text tokens as they arrive.</summary>
    /// <returns>
    /// stop_reason string, accumulated text content, and any tool_use blocks.
    /// </returns>
    private async Task<(string StopReason, string TextContent, List<ToolUse> ToolUses)>
        StreamSseAsync(HttpResponseMessage response, ChannelWriter<string> output, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader       = new StreamReader(stream, Encoding.UTF8);

        var textBuilder  = new StringBuilder();
        var stopReason   = "end_turn";
        var toolUses     = new List<ToolUse>();
        ToolUse? current = null; // tool_use block being accumulated

        string? line;
        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) != null)
        {
            // SSE format: "data: {json}" lines; blank lines are separators.
            if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;

            var json = line["data: ".Length..];
            if (json == "[DONE]") break;

            JsonElement evt;
            try   { evt = JsonDocument.Parse(json).RootElement; }
            catch { continue; }

            if (!evt.TryGetProperty("type", out var typeProp)) continue;
            var type = typeProp.GetString();

            switch (type)
            {
                case "content_block_start":
                {
                    if (!evt.TryGetProperty("content_block", out var block)) break;
                    var blockType = block.TryGetProperty("type", out var bt) ? bt.GetString() : null;

                    if (blockType == "tool_use")
                    {
                        var id   = block.TryGetProperty("id",   out var idProp)   ? idProp.GetString()   ?? "" : "";
                        var name = block.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                        current  = new ToolUse(id, name);
                        toolUses.Add(current);
                    }
                    break;
                }

                case "content_block_delta":
                {
                    if (!evt.TryGetProperty("delta", out var delta)) break;
                    var deltaType = delta.TryGetProperty("type", out var dt) ? dt.GetString() : null;

                    if (deltaType == "text_delta" && delta.TryGetProperty("text", out var textProp))
                    {
                        var text = textProp.GetString() ?? "";
                        textBuilder.Append(text);
                        // Emit each token immediately — no buffering.
                        if (text.Length > 0)
                            output.TryWrite($"[{DateTime.UtcNow:o}] {text}");
                    }
                    else if (deltaType == "input_json_delta" && current != null
                             && delta.TryGetProperty("partial_json", out var partialProp))
                    {
                        current.InputJson.Append(partialProp.GetString());
                    }
                    break;
                }

                case "message_delta":
                {
                    if (evt.TryGetProperty("delta", out var delta)
                        && delta.TryGetProperty("stop_reason", out var sr))
                    {
                        stopReason = sr.GetString() ?? "end_turn";

                        if (evt.TryGetProperty("usage", out var usage))
                        {
                            var inputTokens  = usage.TryGetProperty("input_tokens",  out var it) ? it.GetInt32() : 0;
                            var outputTokens = usage.TryGetProperty("output_tokens", out var ot) ? ot.GetInt32() : 0;
                            output.TryWrite(
                                $"[{DateTime.UtcNow:o}] [anthropic] tokens: {inputTokens} prompt / {outputTokens} generated");
                        }
                    }
                    break;
                }
            }
        }

        // Finalise: snapshot mutable InputJson builders into immutable strings.
        var completedUses = toolUses
            .Select(u => u with { InputJson = new StringBuilder(u.InputJson.ToString()) })
            .ToList();

        return (stopReason, textBuilder.ToString(), completedUses);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static string BuildRequest(
        string modelId, string systemPrompt, List<JsonNode> messages, bool includeTools)
    {
        var obj = new JsonObject
        {
            ["model"]      = modelId,
            ["max_tokens"] = DefaultMaxTokens,
            ["system"]     = systemPrompt,
            ["messages"]   = new JsonArray(messages.Select(m => m.DeepClone()).ToArray()),
            ["stream"]     = true,
        };

        if (includeTools)
            obj["tools"] = JsonNode.Parse(AnthropicToolSchemas.ToolsJson)!.AsArray();

        return obj.ToJsonString();
    }

    private static void AddHeaders(HttpRequestMessage request, string apiKey)
    {
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
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
    // Tool use accumulator
    // -------------------------------------------------------------------------

    /// <summary>Mutable accumulator for a single tool_use content block during streaming.</summary>
    private sealed record ToolUse(string Id, string Name)
    {
        /// <summary>Accumulated partial_json deltas — finalised after message_stop.</summary>
        public StringBuilder InputJson { get; init; } = new();
    }
}
