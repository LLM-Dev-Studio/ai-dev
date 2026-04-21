using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using AiDev.Services;
using Microsoft.Extensions.Logging;

namespace AiDev.Executors;

/// <summary>
/// Runs an agent session via the LM Studio HTTP API.
///
/// Health checks use the native <c>/api/v1/models</c> endpoint to discover loaded models.
/// Inference uses the OpenAI-compatible <c>/v1/chat/completions</c> endpoint which supports
/// streaming and function calling (the native <c>/api/v1/chat</c> does not support custom tools).
///
/// When the "mcp-workspace" skill is enabled, workspace tool schemas are included in the
/// request (OpenAI function-calling format, same as the Ollama and GitHub Models executors).
/// The executor handles tool_calls returned by the model, executes the corresponding workspace
/// operations, appends results to the message history, and re-invokes the model — continuing
/// until the model produces a final response with no further tool calls.
/// </summary>
public sealed class LmStudioAgentExecutor(
    IHttpClientFactory httpClientFactory,
    StudioSettingsService settingsService,
    ILogger<LmStudioAgentExecutor> logger) : IAgentExecutor
{
    public string Name        => "lmstudio";
    public string DisplayName => "LM Studio";

    public IReadOnlyList<ExecutorSkill> AvailableSkills { get; } = LmStudioSkills.All;

    /// <summary>
    /// LM Studio models are loaded locally — none are known ahead of time.
    /// The health check discovers them dynamically from /api/v1/models.
    /// </summary>
    public IReadOnlyList<ModelDescriptor> KnownModels => [];

    private const int MaxToolIterations = 30;
    private const int DefaultMaxTokens  = 4096;

    // Cache of context-window sizes per model, populated from the /api/v1/models
    // health-check response. Used by RunAsync to preflight requests against the
    // loaded context window and avoid fruitless calls to LM Studio.
    private readonly ConcurrentDictionary<string, int> _contextWindows = new(StringComparer.OrdinalIgnoreCase);

    // -------------------------------------------------------------------------
    // Health check
    // -------------------------------------------------------------------------

    public async Task<ExecutorHealthResult> CheckHealthAsync(CancellationToken ct = default)
    {
        var baseUrl = settingsService.GetSettings().LmStudioBaseUrl.TrimEnd('/');
        var candidateUrls = new[]
        {
            $"{baseUrl}/api/v1/models", // LM Studio native
            $"{baseUrl}/v1/models",     // OpenAI-compatible
        };

        try
        {
            var http = httpClientFactory.CreateClient("lmstudio-health");

            foreach (var url in candidateUrls)
            {
                using var response = await http.GetAsync(url, ct).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    // Try alternate shape if this endpoint does not exist.
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        continue;

                    return new ExecutorHealthResult(false, $"LM Studio returned HTTP {(int)response.StatusCode}");
                }

                List<ModelDescriptor> discovered = [];
                try
                {
                    using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>(ct).ConfigureAwait(false);
                    if (doc is not null)
                    {
                        var root = doc.RootElement;

                        // Native shape: { "models": [...] }
                        // OpenAI-compatible shape: { "data": [...] }
                        JsonElement modelsArray;
                        if (root.TryGetProperty("models", out modelsArray) && modelsArray.ValueKind == JsonValueKind.Array)
                        {
                            discovered = ParseModelDescriptors(modelsArray);
                        }
                        else if (root.TryGetProperty("data", out modelsArray) && modelsArray.ValueKind == JsonValueKind.Array)
                        {
                            discovered = ParseModelDescriptors(modelsArray);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "[lmstudio-health] Failed to parse model list from {Url}", url);
                }

                var message = discovered.Count > 0
                    ? $"{discovered.Count} model(s) available"
                    : "Connected (no models loaded)";

                return new ExecutorHealthResult(true, message, discovered);
            }

            return new ExecutorHealthResult(false, "LM Studio model endpoint not found (/api/v1/models or /v1/models)");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[lmstudio-health] Probe failed: {Message}", ex.Message);
            return new ExecutorHealthResult(false, $"Connection refused: {ex.Message}");
        }
    }

    private List<ModelDescriptor> ParseModelDescriptors(JsonElement models)
    {
        var result = new List<ModelDescriptor>();

        foreach (var m in models.EnumerateArray())
        {
            // Only include LLMs, not embedding models.
            if (m.TryGetProperty("type", out var typeElement)
                && string.Equals(typeElement.GetString(), "embedding", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var id = GetString(m, "key") ?? GetString(m, "id") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var name = GetString(m, "display_name") ?? GetString(m, "name") ?? id;

            var caps = ModelCapabilities.Streaming;
            if (m.TryGetProperty("capabilities", out var capObj) && capObj.ValueKind == JsonValueKind.Object)
            {
                if (TryGetBoolean(capObj, "trained_for_tool_use", out var canTools) && canTools)
                    caps |= ModelCapabilities.ToolCalling;
                if (TryGetBoolean(capObj, "vision", out var canVision) && canVision)
                    caps |= ModelCapabilities.Vision;
            }

            // LM Studio exposes both the loaded context window (for currently-loaded models) and
            // the model's maximum supported context. Prefer the loaded value — it reflects what
            // was actually allocated, which is what matters for preflight budget checks.
            var contextWindow =
                   TryGetInt32(m, "loaded_context_length")
                ?? TryGetInt32(m, "max_context_length")
                ?? TryGetInt32(m, "context_length")
                ?? 0;

            if (contextWindow > 0)
                _contextWindows[id] = contextWindow;

            result.Add(new ModelDescriptor(id, name, "lmstudio", caps, ContextWindow: contextWindow));
        }

        return result;
    }

    private static string? GetString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool TryGetBoolean(JsonElement element, string propertyName, out bool value)
    {
        value = false;
        if (!element.TryGetProperty(propertyName, out var property))
            return false;

        if (property.ValueKind == JsonValueKind.True)
        {
            value = true;
            return true;
        }

        if (property.ValueKind == JsonValueKind.False)
        {
            value = false;
            return true;
        }

        if (property.ValueKind == JsonValueKind.String && bool.TryParse(property.GetString(), out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static int? TryGetInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
            return null;

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var n))
            return n;

        if (prop.ValueKind == JsonValueKind.String
            && int.TryParse(prop.GetString(), out var parsed))
            return parsed;

        return null;
    }

    // -------------------------------------------------------------------------
    // Run
    // -------------------------------------------------------------------------

    public async Task<ExecutorResult> RunAsync(ExecutorContext context, ChannelWriter<string> output)
    {
        using var activity = AiDevTelemetry.ActivitySource.StartActivity("Executor.LmStudio.Run", ActivityKind.Client);
        activity?.SetTag("executor.name", Name);
        activity?.SetTag("project.slug", context.Trigger?.ProjectSlug);
        activity?.SetTag("task.id", context.Trigger?.TaskId);
        activity?.SetTag("decision.id", context.Trigger?.DecisionId);
        activity?.SetTag("agent.trigger.source", context.Trigger?.Source);
        activity?.SetTag("agent.trigger.reason", context.Trigger?.Reason);
        activity?.SetTag("message.file", context.Trigger?.MessageFile);

        var systemPrompt = BuildSystemPrompt(context.WorkingDir);
        var rawBaseUrl   = settingsService.GetSettings().LmStudioBaseUrl.TrimEnd('/');

        if (!Uri.TryCreate(rawBaseUrl, UriKind.Absolute, out var parsedUri)
            || (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps))
        {
            var msg = $"LmStudioBaseUrl '{rawBaseUrl}' is not a valid http/https URL. Aborting.";
            logger.LogError("[lmstudio] {Message}", msg);
            output.TryWrite($"[{DateTime.UtcNow:o}] [error] {msg}");
            return new ExecutorResult(1, ErrorMessage: msg);
        }

        // OpenAI-compatible endpoint for inference (supports tool calling).
        var requestUri    = $"{rawBaseUrl}/v1/chat/completions";
        var enableTools   = LmStudioSkills.AreWorkspaceToolsEnabled(context.EnabledSkills);
        var workspaceRoot = enableTools ? DeriveWorkspaceRoot(context.WorkingDir) : null;

        logger.LogInformation(
            "[lmstudio] Starting session — model={Model} endpoint={Uri} tools={Tools}",
            context.ModelId, requestUri, enableTools ? "enabled" : "disabled");
        logger.LogInformation(
            "[lmstudio] Trigger source={Source} reason={Reason} project={Project} task={TaskId} decision={DecisionId} message={MessageFile}",
            context.Trigger?.Source,
            context.Trigger?.Reason,
            context.Trigger?.ProjectSlug,
            context.Trigger?.TaskId,
            context.Trigger?.DecisionId,
            context.Trigger?.MessageFile);
        output.TryWrite($"[{DateTime.UtcNow:o}] [lmstudio] model={context.ModelId} endpoint={requestUri}");

        if (enableTools)
            output.TryWrite($"[{DateTime.UtcNow:o}] [lmstudio] workspace tools enabled — root={workspaceRoot}");

        // Build mutable message history for the tool execution loop.
        var messages = new List<JsonNode>
        {
            new JsonObject { ["role"] = "system", ["content"] = systemPrompt },
            new JsonObject { ["role"] = "user",   ["content"] = context.Prompt },
        };

        // Resolve the model's loaded context window. If the cache is empty (e.g. the user
        // skipped the health check UI), trigger a probe to populate it. A context of 0 means
        // "unknown" and the preflight will skip — we then rely on LM Studio to report errors.
        var contextWindow = await ResolveContextWindowAsync(context.ModelId, context.CancellationToken)
            .ConfigureAwait(false);
        var maxOutputTokens = TokenBudget.RecommendMaxOutputTokens(contextWindow, floor: 512, ceiling: DefaultMaxTokens);
        var toolsJson       = enableTools ? OllamaToolSchemas.GetToolsArray().ToJsonString() : null;

        if (contextWindow > 0)
            output.TryWrite(
                $"[{DateTime.UtcNow:o}] [lmstudio] context_window={contextWindow} max_output_tokens={maxOutputTokens}");

        var http      = httpClientFactory.CreateClient("lmstudio");
        int iteration = 0;
        TokenUsage? totalUsage = null;

        while (iteration++ < MaxToolIterations)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            // Preflight: estimate token budget before each request. Because the messages list
            // grows with tool_call/tool_result pairs, an overflow can appear mid-session even
            // after the first iteration fits.
            var preflight = TokenBudget.Preflight(
                contextWindow:   contextWindow,
                maxOutputTokens: maxOutputTokens,
                messageContents: messages.Select(ExtractMessageContent),
                toolsJson:       toolsJson,
                modelId:         context.ModelId,
                executorName:    DisplayName);

            if (!preflight.Fits)
            {
                var msg = iteration == 1
                    ? preflight.Error!
                    : preflight.Error + $" (after {iteration - 1} tool iteration(s))";
                logger.LogError("[lmstudio] {Message}", msg);
                output.TryWrite($"[{DateTime.UtcNow:o}] [error] {msg}");
                return new ExecutorResult(1, Usage: totalUsage, ErrorMessage: msg);
            }

            var requestBody = BuildRequest(context.ModelId, messages, enableTools, maxOutputTokens);

            HttpResponseMessage response;
            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(requestBody, Encoding.UTF8, "application/json"),
                };

                response = await http.SendAsync(
                    httpRequest, HttpCompletionOption.ResponseHeadersRead, context.CancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var msg = $"Failed to connect to LM Studio at {requestUri}: {ex.Message}";
                logger.LogError(ex, "[lmstudio] {Message}", msg);
                output.TryWrite($"[{DateTime.UtcNow:o}] [error] {msg}");
                return new ExecutorResult(1, ErrorMessage: msg);
            }

            using var _ = response;

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(context.CancellationToken).ConfigureAwait(false);
                var msg  = $"LM Studio returned HTTP {(int)response.StatusCode}: {body}";
                logger.LogError("[lmstudio] {Message}", msg);
                output.TryWrite($"[{DateTime.UtcNow:o}] [error] {msg}");
                return new ExecutorResult(1, ErrorMessage: msg);
            }

            logger.LogInformation("[lmstudio] Streaming response — iteration {N}", iteration);

            // Stream and parse OpenAI SSE response.
            var (finishReason, textContent, toolCalls, sessionUsage) =
                await StreamSseAsync(response, output, context.CancellationToken).ConfigureAwait(false);

            // Accumulate usage across all tool-call iterations.
            if (sessionUsage != null)
                totalUsage = totalUsage == null ? sessionUsage : totalUsage + sessionUsage;

            logger.LogInformation("[lmstudio] finish_reason={Reason} iteration={N}", finishReason, iteration);

            // Final response: no tool calls.
            if (toolCalls.Count == 0)
            {
                logger.LogInformation(
                    "[lmstudio] Session complete — {Chars} chars | iterations: {N}",
                    textContent.Length, iteration);
                return new ExecutorResult(0, Usage: totalUsage);
            }

            // Tool calls requested — execute and loop.
            logger.LogInformation(
                "[lmstudio] Tool calls requested — count={Count} iteration={N}", toolCalls.Count, iteration);
            output.TryWrite($"[{DateTime.UtcNow:o}] [lmstudio] tool calls: {toolCalls.Count} (iteration {iteration})");

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
                var argsPreview = argsJson.Length > 80 ? argsJson[..80] + "..." : argsJson;
                output.TryWrite($"[{DateTime.UtcNow:o}] [tool:call] {tc.Name}({argsPreview})");
                logger.LogInformation("[lmstudio] Executing tool — {Tool}({Args})", tc.Name, argsPreview);

                JsonElement argsElement;
                try   { argsElement = JsonDocument.Parse(argsJson).RootElement; }
                catch { argsElement = JsonDocument.Parse("{}").RootElement; }

                var result = workspaceRoot != null
                    ? WorkspaceTools.Execute(workspaceRoot, tc.Name, argsElement)
                    : "[error] mcp-workspace not enabled";

                var preview = result.Length > 120
                    ? result[..120].Replace('\n', ' ') + "..."
                    : result.Replace('\n', ' ');
                output.TryWrite($"[{DateTime.UtcNow:o}] [tool:result] {preview}");
                logger.LogInformation("[lmstudio] Tool result — {Chars} chars", result.Length);

                messages.Add(new JsonObject
                {
                    ["role"]         = "tool",
                    ["tool_call_id"] = tc.Id,
                    ["content"]      = result,
                });
            }
        }

        var limitMsg = $"Exceeded maximum tool-call iterations ({MaxToolIterations}). Aborting session.";
        logger.LogError("[lmstudio] {Message}", limitMsg);
        output.TryWrite($"[{DateTime.UtcNow:o}] [error] {limitMsg}");
        return new ExecutorResult(1, ErrorMessage: limitMsg);
    }

    // -------------------------------------------------------------------------
    // SSE streaming
    // -------------------------------------------------------------------------

    /// <summary>
    /// Streams the OpenAI-format SSE response, emitting text tokens as they arrive.
    /// </summary>
    private async Task<(string FinishReason, string TextContent, List<ToolCall> ToolCalls, TokenUsage? Usage)>
        StreamSseAsync(HttpResponseMessage response, ChannelWriter<string> output, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader       = new StreamReader(stream, Encoding.UTF8);

        var textBuilder  = new StringBuilder();
        var finishReason = string.Empty;
        var toolCalls    = new Dictionary<int, ToolCall>(); // index -> accumulator
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
                        var id = tcDelta.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
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

            // Usage on the final chunk
            if (evt.TryGetProperty("usage", out var usage) && usage.ValueKind != JsonValueKind.Null)
            {
                var promptTokens = usage.TryGetProperty("prompt_tokens",     out var pt)  ? pt.GetInt32()  : 0;
                var outputTokens = usage.TryGetProperty("completion_tokens", out var ct2) ? ct2.GetInt32() : 0;
                if (promptTokens > 0 || outputTokens > 0)
                {
                    capturedUsage = new TokenUsage(promptTokens, outputTokens);
                    output.TryWrite(
                        $"[{DateTime.UtcNow:o}] [lmstudio] tokens: {promptTokens} prompt / {outputTokens} generated");
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

    private static string BuildRequest(string modelId, List<JsonNode> messages, bool includeTools, int maxOutputTokens)
    {
        var obj = new JsonObject
        {
            ["model"]      = modelId,
            ["max_tokens"] = maxOutputTokens,
            ["messages"]   = new JsonArray(messages.Select(m => m.DeepClone()).ToArray()),
            ["stream"]     = true,
            ["stream_options"] = new JsonObject { ["include_usage"] = true },
        };

        if (includeTools)
            obj["tools"] = OllamaToolSchemas.GetToolsArray();

        return obj.ToJsonString();
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

    /// <summary>
    /// Extract a best-effort string representation of a message's billable content
    /// for token estimation. Includes the content field plus any tool_call arguments.
    /// </summary>
    private static string ExtractMessageContent(JsonNode message)
    {
        if (message is not JsonObject obj) return message.ToJsonString();

        var sb = new StringBuilder();

        if (obj["content"] is JsonNode content && content is not null)
        {
            sb.Append(content is JsonValue v && v.TryGetValue<string>(out var s)
                ? s
                : content.ToJsonString());
        }

        if (obj["tool_calls"] is JsonArray toolCalls)
            sb.Append(toolCalls.ToJsonString());

        return sb.ToString();
    }

    /// <summary>
    /// Look up the cached context window for a model. If unknown, trigger a one-shot
    /// health-check probe to populate the cache. Returns 0 when the value cannot be resolved.
    /// </summary>
    private async Task<int> ResolveContextWindowAsync(string modelId, CancellationToken ct)
    {
        if (_contextWindows.TryGetValue(modelId, out var cached))
            return cached;

        try
        {
            _ = await CheckHealthAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "[lmstudio] On-demand health probe failed while resolving context window");
        }

        return _contextWindows.TryGetValue(modelId, out var resolved) ? resolved : 0;
    }

    // -------------------------------------------------------------------------
    // Tool call accumulator
    // -------------------------------------------------------------------------

    private sealed class ToolCall(string id)
    {
        public string        Id        { get; set; } = id;
        public string        Name      { get; set; } = string.Empty;
        public StringBuilder Arguments { get; }      = new();
    }
}
