using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using AiDev.Services;
using Microsoft.Extensions.Logging;

namespace AiDev.Executors;

/// <summary>
/// Runs an agent session via the Ollama HTTP API (/api/chat).
/// Reads CLAUDE.md from the agent directory as the system prompt, then streams
/// the model's response to the output channel.
///
/// When the "mcp-workspace" skill is enabled, workspace tool schemas are included
/// in the request. The executor handles tool_calls returned by the model, executes
/// the corresponding workspace operations, appends results to the message history,
/// and re-invokes the model — continuing until the model produces a final response
/// with no further tool calls.
///
/// Tool implementations live in <see cref="WorkspaceTools"/> and delegate directly
/// to the MCP server implementation without the protocol overhead.
/// </summary>
public class OllamaAgentExecutor(
    IHttpClientFactory httpClientFactory,
    StudioSettingsService settingsService,
    ILogger<OllamaAgentExecutor> logger) : IAgentExecutor
{
    public string Name        => "ollama";
    public string DisplayName => "Ollama";

    public IReadOnlyList<ExecutorSkill> AvailableSkills { get; } =
    [
        new ExecutorSkill(
            Key:            "mcp-workspace",
            DisplayName:    "Workspace Tools",
            Description:    "Grants read/write access to board, inbox, journal, KB, and decision files via direct filesystem calls.",
            DefaultEnabled: true),
    ];

    /// <summary>
    /// Ollama models are installed locally — none are known ahead of time.
    /// The health check discovers them dynamically from /api/tags.
    /// </summary>
    public IReadOnlyList<ModelDescriptor> KnownModels => [];

    // Maximum number of tool-call/response iterations per session.
    // Prevents runaway loops if the model repeatedly calls tools.
    private const int MaxToolIterations = 30;

    // Ollama's own default num_ctx is 2048. We ask for up to this ceiling when the model
    // advertises a larger context via /api/show, subject to TokenBudget's output reservation.
    private const int DefaultMaxOutputTokens = 4096;

    // Cache of context-window sizes per model id, populated lazily from /api/show.
    private readonly ConcurrentDictionary<string, int> _contextWindows = new(StringComparer.OrdinalIgnoreCase);

    // -------------------------------------------------------------------------
    // Health check
    // -------------------------------------------------------------------------

    public async Task<ExecutorHealthResult> CheckHealthAsync(CancellationToken ct = default)
    {
        var baseUrl = settingsService.GetSettings().OllamaBaseUrl.TrimEnd('/');
        var url     = $"{baseUrl}/api/tags";

        try
        {
            var http     = httpClientFactory.CreateClient("ollama-health");
            var response = await http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return new ExecutorHealthResult(false, $"Ollama returned HTTP {(int)response.StatusCode}");

            List<string> names = [];
            try
            {
                var doc = await response.Content.ReadFromJsonAsync<JsonDocument>(ct).ConfigureAwait(false);
                names = doc?.RootElement.GetProperty("models")
                    .EnumerateArray()
                    .Select(m => m.GetProperty("name").GetString() ?? string.Empty)
                    .Where(n => n.Length > 0)
                    .ToList() ?? [];
            }
            catch { /* model list is best-effort */ }

            // Fetch context_length for each model in parallel via /api/show.
            // /api/tags doesn't include context length, but /api/show does (per model).
            var contextLookups = names.Select(n => FetchContextLengthAsync(baseUrl, n, ct)).ToArray();
            var contextLengths = await Task.WhenAll(contextLookups).ConfigureAwait(false);

            var discovered = new List<ModelDescriptor>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                var name = names[i];
                var ctxLen = contextLengths[i];
                if (ctxLen > 0)
                    _contextWindows[name] = ctxLen;

                discovered.Add(new ModelDescriptor(
                    name, name, "ollama",
                    ModelCapabilities.Streaming | ModelCapabilities.ToolCalling,
                    ContextWindow: ctxLen));
            }

            var message = discovered.Count > 0
                ? $"{discovered.Count} model(s) available"
                : "Connected (no models found)";

            return new ExecutorHealthResult(true, message, discovered);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[ollama-health] Probe failed: {Message}", ex.Message);
            return new ExecutorHealthResult(false, $"Connection refused: {ex.Message}");
        }
    }

    /// <summary>
    /// Query Ollama's /api/show endpoint for a model's context length. The field name is
    /// architecture-specific (e.g. "llama.context_length", "qwen2.context_length"), so we
    /// scan model_info for any property ending in ".context_length". Returns 0 when the
    /// lookup fails — callers treat that as "unknown" and skip preflight checks.
    /// </summary>
    private async Task<int> FetchContextLengthAsync(string baseUrl, string modelName, CancellationToken ct)
    {
        try
        {
            var http = httpClientFactory.CreateClient("ollama-health");
            var body = new StringContent(
                JsonSerializer.Serialize(new { name = modelName }),
                Encoding.UTF8,
                "application/json");

            using var response = await http.PostAsync($"{baseUrl}/api/show", body, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return 0;

            using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>(ct).ConfigureAwait(false);
            if (doc is null) return 0;

            if (!doc.RootElement.TryGetProperty("model_info", out var info) ||
                info.ValueKind != JsonValueKind.Object)
                return 0;

            foreach (var prop in info.EnumerateObject())
            {
                if (!prop.Name.EndsWith(".context_length", StringComparison.Ordinal))
                    continue;

                if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var n))
                    return n;
                if (prop.Value.ValueKind == JsonValueKind.String
                    && int.TryParse(prop.Value.GetString(), out var parsed))
                    return parsed;
            }

            return 0;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[ollama-health] /api/show failed for {Model}", modelName);
            return 0;
        }
    }

    // -------------------------------------------------------------------------
    // Run
    // -------------------------------------------------------------------------

    public async Task<ExecutorResult> RunAsync(ExecutorContext context, ChannelWriter<string> output)
    {
        using var activity = AiDevTelemetry.ActivitySource.StartActivity("Executor.Ollama.Run", ActivityKind.Client);
        activity?.SetTag("executor.name", Name);
        activity?.SetTag("project.slug", context.Trigger?.ProjectSlug);
        activity?.SetTag("task.id", context.Trigger?.TaskId);
        activity?.SetTag("decision.id", context.Trigger?.DecisionId);
        activity?.SetTag("agent.trigger.source", context.Trigger?.Source);
        activity?.SetTag("agent.trigger.reason", context.Trigger?.Reason);
        activity?.SetTag("message.file", context.Trigger?.MessageFile);

        var systemPrompt = BuildSystemPrompt(context.WorkingDir);
        var rawBaseUrl   = settingsService.GetSettings().OllamaBaseUrl.TrimEnd('/');

        if (!Uri.TryCreate(rawBaseUrl, UriKind.Absolute, out var parsedUri)
            || (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps))
        {
            var msg = $"OllamaBaseUrl '{rawBaseUrl}' is not a valid http/https URL. Aborting.";
            logger.LogError("[ollama] {Message}", msg);
            output.TryWrite($"[{DateTime.UtcNow:o}] [error] {msg}");
            return new ExecutorResult(1, ErrorMessage: msg);
        }

        var requestUri    = $"{rawBaseUrl}/api/chat";
        var enableTools   = OllamaToolSupport.AreWorkspaceToolsEnabled(context.EnabledSkills);
        var workspaceRoot = enableTools ? DeriveWorkspaceRoot(context.WorkingDir) : null;

        if (enableTools && OllamaToolSupport.IsKnownUnsupportedModel(context.ModelId))
        {
            var msg = OllamaToolSupport.GetUnsupportedToolsMessage(context.ModelId);
            logger.LogWarning("[ollama] {Message}", msg);
            output.TryWrite($"[{DateTime.UtcNow:o}] [error] {msg}");
            return new ExecutorResult(1, PreserveInbox: true, ErrorMessage: msg);
        }

        logger.LogInformation(
            "[ollama] Starting session — model={Model} endpoint={Uri} tools={Tools}",
            context.ModelId, requestUri, enableTools ? "enabled" : "disabled");
        logger.LogInformation(
            "[ollama] Trigger source={Source} reason={Reason} project={Project} task={TaskId} decision={DecisionId} message={MessageFile}",
            context.Trigger?.Source,
            context.Trigger?.Reason,
            context.Trigger?.ProjectSlug,
            context.Trigger?.TaskId,
            context.Trigger?.DecisionId,
            context.Trigger?.MessageFile);
        output.TryWrite($"[{DateTime.UtcNow:o}] [ollama] model={context.ModelId} endpoint={requestUri}");

        if (enableTools)
            output.TryWrite($"[{DateTime.UtcNow:o}] [ollama] workspace tools enabled — root={workspaceRoot}");

        // Build mutable message history for the tool execution loop.
        var messages = new List<JsonNode>
        {
            new JsonObject { ["role"] = "system", ["content"] = systemPrompt },
            new JsonObject { ["role"] = "user",   ["content"] = context.Prompt },
        };

        // Resolve context window (from health-check cache, or probe on demand).
        var contextWindow = await ResolveContextWindowAsync(rawBaseUrl, context.ModelId, context.CancellationToken)
            .ConfigureAwait(false);
        var maxOutputTokens = TokenBudget.RecommendMaxOutputTokens(
            contextWindow, floor: 512, ceiling: DefaultMaxOutputTokens);
        var toolsJson       = enableTools ? OllamaToolSchemas.GetToolsArray().ToJsonString() : null;

        if (contextWindow > 0)
            output.TryWrite(
                $"[{DateTime.UtcNow:o}] [ollama] context_window={contextWindow} max_output_tokens={maxOutputTokens}");

        var http      = httpClientFactory.CreateClient("ollama");
        int iteration = 0;
        TokenUsage? totalUsage = null;

        while (iteration++ < MaxToolIterations)
        {
            // Preflight: estimate the token budget before each request. The messages list
            // grows with tool_call/tool_result pairs, so an overflow can appear mid-session.
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
                logger.LogError("[ollama] {Message}", msg);
                output.TryWrite($"[{DateTime.UtcNow:o}] [error] {msg}");
                return new ExecutorResult(1, Usage: totalUsage, ErrorMessage: msg);
            }

            // Serialize the current message list into a fresh JsonArray
            // (JsonNode instances can only belong to one parent, so we deep-clone).
            var requestObj = new JsonObject
            {
                ["model"]    = context.ModelId,
                ["messages"] = new JsonArray(messages.Select(m => m.DeepClone()).ToArray()),
                ["stream"]   = true,
            };

            // Ollama's default num_ctx is 2048 regardless of what the model was trained for.
            // Tell it explicitly to allocate the context window we discovered via /api/show,
            // so we don't silently truncate large prompts.
            if (contextWindow > 0)
            {
                requestObj["options"] = new JsonObject
                {
                    ["num_ctx"]     = contextWindow,
                    ["num_predict"] = maxOutputTokens,
                };
            }

            if (enableTools)
                requestObj["tools"] = OllamaToolSchemas.GetToolsArray();

            var json = requestObj.ToJsonString();

            // Send request and read streaming response.
            HttpResponseMessage response;
            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
                };
                // ResponseHeadersRead returns as soon as headers arrive so we can stream the body.
                response = await http.SendAsync(
                    httpRequest, HttpCompletionOption.ResponseHeadersRead, context.CancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var msg = $"Failed to connect to Ollama at {requestUri}: {ex.Message}";
                logger.LogError(ex, "[ollama] {Message}", msg);
                output.TryWrite($"[{DateTime.UtcNow:o}] [error] {msg}");
                return new ExecutorResult(1, ErrorMessage: msg);
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(context.CancellationToken).ConfigureAwait(false);
                if (enableTools && OllamaToolSupport.TryGetUnsupportedToolsMessage(context.ModelId, body, out var unsupportedToolsMessage))
                {
                    logger.LogWarning("[ollama] Request rejected because workspace tools are unsupported for model {Model}: {Body}",
                        context.ModelId, body);
                    output.TryWrite($"[{DateTime.UtcNow:o}] [error] {unsupportedToolsMessage}");
                    return new ExecutorResult(1, PreserveInbox: true, ErrorMessage: unsupportedToolsMessage);
                }

                var msg  = $"Ollama returned HTTP {(int)response.StatusCode}: {body}";
                logger.LogError("[ollama] {Message}", msg);
                output.TryWrite($"[{DateTime.UtcNow:o}] [error] {msg}");
                return new ExecutorResult(1, ErrorMessage: msg);
            }

            logger.LogInformation("[ollama] Streaming response — iteration {N}", iteration);

            // --- Stream the response body ---
            await using var stream = await response.Content.ReadAsStreamAsync(context.CancellationToken).ConfigureAwait(false);
            using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8);

            var contentBuilder    = new System.Text.StringBuilder();
            var lineBuffer        = new System.Text.StringBuilder(); // buffer partial lines for output
            OllamaChatChunk? finalChunk = null;
            string? line;

            while ((line = await reader.ReadLineAsync(context.CancellationToken).ConfigureAwait(false)) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                OllamaChatChunk? chunk;
                try { chunk = JsonSerializer.Deserialize<OllamaChatChunk>(line, OllamaJsonOptions); }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "[ollama] Failed to parse chunk: {Line}", line);
                    continue;
                }

                if (chunk?.Message?.Content is { Length: > 0 } token)
                {
                    contentBuilder.Append(token);

                    // Emit complete lines as they arrive; buffer partial lines.
                    lineBuffer.Append(token);
                    var buffered = lineBuffer.ToString();
                    var newlineIdx = buffered.LastIndexOf('\n');
                    if (newlineIdx >= 0)
                    {
                        // Emit all complete lines in the buffer.
                        var complete = buffered[..newlineIdx];
                        foreach (var outputLine in complete.Split('\n'))
                            output.TryWrite($"[{DateTime.UtcNow:o}] {outputLine}");
                        lineBuffer.Clear();
                        lineBuffer.Append(buffered[(newlineIdx + 1)..]);
                    }
                }

                if (chunk?.Done == true)
                {
                    finalChunk = chunk;
                    break;
                }
            }

            // Flush any remaining partial line.
            if (lineBuffer.Length > 0)
                output.TryWrite($"[{DateTime.UtcNow:o}] {lineBuffer}");

            var responseText = contentBuilder.ToString().Trim();

            // --- Check for tool calls ---
            var toolCalls = finalChunk?.Message?.ToolCalls;

            if (toolCalls is { Count: > 0 } && enableTools && workspaceRoot != null)
            {
                logger.LogInformation("[ollama] Tool calls requested — count={Count} iteration={N}", toolCalls.Count, iteration);
                output.TryWrite($"[{DateTime.UtcNow:o}] [ollama] tool calls: {toolCalls.Count} (iteration {iteration})");

                // Accumulate token usage from this tool-calling iteration before continuing.
                var iterPrompt = finalChunk?.PromptEvalCount ?? 0;
                var iterOutput = finalChunk?.EvalCount       ?? 0;
                if (iterPrompt > 0 || iterOutput > 0)
                {
                    var iterUsage = new TokenUsage(iterPrompt, iterOutput);
                    totalUsage = totalUsage == null ? iterUsage : totalUsage + iterUsage;
                }

                // Add the assistant message (may include partial content + tool_calls) to history.
                // Re-serialise the tool_calls through the snake_case options so the JSON matches
                // what Ollama expects when we send the history back.
                var toolCallsJson  = JsonSerializer.Serialize(toolCalls, OllamaJsonOptions);
                var toolCallsNode  = JsonNode.Parse(toolCallsJson)!;
                var assistantMsg   = new JsonObject
                {
                    ["role"]       = "assistant",
                    ["content"]    = responseText,
                    ["tool_calls"] = toolCallsNode,
                };
                messages.Add(assistantMsg);

                // Execute each tool and append result messages.
                foreach (var call in toolCalls)
                {
                    var toolName = call.Function?.Name ?? "unknown";
                    var toolArgs = call.Function?.Arguments ?? default;
                    var argsPreview = ArgsPreview(toolArgs);

                    output.TryWrite($"[{DateTime.UtcNow:o}] [tool:call] {toolName}({argsPreview})");
                    logger.LogInformation("[ollama] Executing tool — {Tool}({Args})", toolName, argsPreview);

                    var result = WorkspaceTools.Execute(workspaceRoot, toolName, toolArgs);

                    var resultPreview = result.Length > 120 ? result[..120].Replace('\n', ' ') + "…" : result.Replace('\n', ' ');
                    output.TryWrite($"[{DateTime.UtcNow:o}] [tool:result] {resultPreview}");
                    logger.LogInformation("[ollama] Tool result — {Chars} chars", result.Length);

                    messages.Add(new JsonObject { ["role"] = "tool", ["content"] = result });
                }

                // Loop — model will continue with the tool results in context.
                continue;
            }

            // --- Final response (no tool calls) ---

            // Stream ended without done=true; emit whatever was collected.
            if (finalChunk == null)
                logger.LogWarning("[ollama] Stream ended without done=true");

            var promptTokens = finalChunk?.PromptEvalCount ?? 0;
            var outputTokens = finalChunk?.EvalCount       ?? 0;
            var durationMs   = finalChunk?.TotalDuration.HasValue == true
                ? finalChunk.TotalDuration.Value / 1_000_000.0
                : 0;

            logger.LogInformation(
                "[ollama] Session complete — {Chars} chars | tokens: {In} in / {Out} out | {Ms:F0} ms | iterations: {N}",
                responseText.Length, promptTokens, outputTokens, durationMs, iteration);

            if (promptTokens > 0 || outputTokens > 0)
                output.TryWrite(
                    $"[{DateTime.UtcNow:o}] [ollama] tokens: {promptTokens} prompt / {outputTokens} generated | {durationMs:F0} ms");

            var usage = (promptTokens > 0 || outputTokens > 0)
                ? new TokenUsage(promptTokens, outputTokens)
                : null;

            // Accumulate usage across all tool-call iterations.
            if (usage != null)
                totalUsage = totalUsage == null ? usage : totalUsage + usage;

            return new ExecutorResult(0, Usage: totalUsage);
        }

        // Reached MaxToolIterations without a final response.
        var limitMsg = $"Exceeded maximum tool-call iterations ({MaxToolIterations}). Aborting session.";
        logger.LogError("[ollama] {Message}", limitMsg);
        output.TryWrite($"[{DateTime.UtcNow:o}] [error] {limitMsg}");
        return new ExecutorResult(1, ErrorMessage: limitMsg);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the workspace root path given the agent's working directory.
    /// WorkingDir is {workspace}/agents/{slug} — the workspace root is 2 levels up.
    /// </summary>
    private static string DeriveWorkspaceRoot(string workingDir) =>
        Path.GetFullPath(Path.Combine(workingDir, "../.."));

    private static string BuildSystemPrompt(string workingDir)
    {
        var claudeMd = Path.Combine(workingDir, "CLAUDE.md");
        if (!File.Exists(claudeMd)) return "You are a helpful AI agent.";
        try { return File.ReadAllText(claudeMd, System.Text.Encoding.UTF8); }
        catch { return "You are a helpful AI agent."; }
    }

    /// <summary>
    /// Extract a best-effort string representation of a message's billable content
    /// for token estimation. Includes the content field plus any tool_call arguments.
    /// </summary>
    private static string ExtractMessageContent(JsonNode message)
    {
        if (message is not JsonObject obj) return message.ToJsonString();

        var sb = new StringBuilder();

        if (obj["content"] is JsonNode content)
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
    /// Return the cached context window for a model or probe /api/show on demand.
    /// Returns 0 when the value cannot be determined — preflight then becomes a no-op.
    /// </summary>
    private async Task<int> ResolveContextWindowAsync(string baseUrl, string modelId, CancellationToken ct)
    {
        if (_contextWindows.TryGetValue(modelId, out var cached))
            return cached;

        var resolved = await FetchContextLengthAsync(baseUrl, modelId, ct).ConfigureAwait(false);
        if (resolved > 0)
            _contextWindows[modelId] = resolved;

        return resolved;
    }

    private static string ArgsPreview(JsonElement args)
    {
        if (args.ValueKind == JsonValueKind.Undefined) return "";
        var raw = args.GetRawText();
        return raw.Length > 80 ? raw[..80] + "…" : raw;
    }

    private static readonly JsonSerializerOptions OllamaJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    // -------------------------------------------------------------------------
    // Ollama response model
    // -------------------------------------------------------------------------

    private sealed class OllamaChatChunk
    {
        public OllamaMessage? Message    { get; set; }
        public bool           Done       { get; set; }
        public string?        DoneReason { get; set; }

        // Token usage — only present on the final done:true chunk.
        public int?  PromptEvalCount { get; set; }
        public int?  EvalCount       { get; set; }

        // Wall-clock duration in nanoseconds — only on done:true chunk.
        public long? TotalDuration   { get; set; }
    }

    private sealed class OllamaMessage
    {
        public string?              Content   { get; set; }
        public List<OllamaToolCall>? ToolCalls { get; set; }
    }

    private sealed class OllamaToolCall
    {
        public OllamaToolCallFunction? Function { get; set; }
    }

    private sealed class OllamaToolCallFunction
    {
        public string?      Name      { get; set; }
        public JsonElement  Arguments { get; set; }
    }
}
