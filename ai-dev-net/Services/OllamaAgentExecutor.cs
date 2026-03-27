namespace AiDevNet.Services;

/// <summary>
/// Runs an agent session via the Ollama HTTP API (/api/chat).
/// Reads CLAUDE.md from the agent directory as the system prompt, then streams
/// the model's response to the output channel.
///
/// Unlike the Claude CLI executor, Ollama has no built-in agentic loop — it performs
/// a single inference turn per session. The AgentRunnerService re-launch logic handles
/// repeated invocations when new inbox messages arrive.
/// </summary>
public class OllamaAgentExecutor(
    IHttpClientFactory httpClientFactory,
    StudioSettingsService settingsService,
    ILogger<OllamaAgentExecutor> logger) : IAgentExecutor
{
    public string Name => "ollama";

    public async Task<int> RunAsync(string workingDir, string modelId, string prompt,
        ChannelWriter<string> output, Action<int>? reportPid, CancellationToken ct)
    {
        // reportPid is not applicable — no OS process

        var systemPrompt = BuildSystemPrompt(workingDir);
        var baseUrl = settingsService.GetSettings().OllamaBaseUrl.TrimEnd('/');
        var requestUri = $"{baseUrl}/api/chat";

        logger.LogInformation("[ollama] Starting session — model={Model} endpoint={Uri}", modelId, requestUri);
        output.TryWrite($"[{DateTime.UtcNow:o}] [ollama] model={modelId} endpoint={requestUri}");

        var requestBody = new
        {
            model = modelId,
            messages = new[]
            {
                new { role = "system",    content = systemPrompt },
                new { role = "user",      content = prompt },
            },
            stream = true,
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var http = httpClientFactory.CreateClient("ollama");

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync(requestUri, httpContent, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var msg = $"Failed to connect to Ollama at {requestUri}: {ex.Message}";
            logger.LogError(ex, "[ollama] {Message}", msg);
            output.TryWrite($"[{DateTime.UtcNow:o}] [error] {msg}");
            return 1;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var msg = $"Ollama returned HTTP {(int)response.StatusCode}: {body}";
            logger.LogError("[ollama] {Message}", msg);
            output.TryWrite($"[{DateTime.UtcNow:o}] [error] {msg}");
            return 1;
        }

        // Stream NDJSON response — each line is a JSON object with a "message.content" chunk.
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8);

        var responseBuilder = new System.Text.StringBuilder();
        string? line;

        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            OllamaChatChunk? chunk;
            try { chunk = JsonSerializer.Deserialize<OllamaChatChunk>(line, OllamaJsonOptions); }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "[ollama] Failed to parse chunk: {Line}", line);
                continue;
            }

            if (chunk?.Message?.Content is { Length: > 0 } content)
            {
                responseBuilder.Append(content);
            }

            if (chunk?.Done == true)
            {
                // Flush accumulated response as a single transcript line
                var fullResponse = responseBuilder.ToString().Trim();
                if (fullResponse.Length > 0)
                {
                    foreach (var responseLine in fullResponse.Split('\n'))
                        output.TryWrite($"[{DateTime.UtcNow:o}] {responseLine}");
                }

                logger.LogInformation("[ollama] Session complete — {Chars} chars", fullResponse.Length);
                return 0;
            }
        }

        // Stream ended without a done=true chunk
        logger.LogWarning("[ollama] Stream ended without done=true");
        var partial = responseBuilder.ToString().Trim();
        if (partial.Length > 0)
        {
            foreach (var responseLine in partial.Split('\n'))
                output.TryWrite($"[{DateTime.UtcNow:o}] {responseLine}");
        }
        return 0;
    }

    private static string BuildSystemPrompt(string workingDir)
    {
        var claudeMd = Path.Combine(workingDir, "CLAUDE.md");
        if (!File.Exists(claudeMd)) return "You are a helpful AI agent.";
        try { return File.ReadAllText(claudeMd, System.Text.Encoding.UTF8); }
        catch { return "You are a helpful AI agent."; }
    }

    private static readonly JsonSerializerOptions OllamaJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Minimal deserialization types for the Ollama streaming response.
    private sealed class OllamaChatChunk
    {
        [JsonPropertyName("message")] public OllamaMessage? Message { get; set; }
        [JsonPropertyName("done")]    public bool Done { get; set; }
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("content")] public string? Content { get; set; }
    }
}
