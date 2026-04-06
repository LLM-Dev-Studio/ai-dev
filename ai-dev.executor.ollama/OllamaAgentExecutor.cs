using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using AiDev.Services;
using Microsoft.Extensions.Logging;

namespace AiDev.Executors;

/// <summary>
/// Runs an agent session via the Ollama HTTP API (/api/chat).
/// Reads CLAUDE.md from the agent directory as the system prompt, then streams
/// the model's response to the output channel.
///
/// Unlike the Claude CLI executor, Ollama has no built-in agentic loop — it performs
/// a single inference turn per session. The AgentRunnerService re-launch logic handles
/// repeated invocations when new inbox messages arrive.
///
/// Ollama has no tool-granting concept, so AvailableSkills is empty. Skills are a
/// no-op for this executor and EnabledSkills in the context is ignored.
/// </summary>
public class OllamaAgentExecutor(
    IHttpClientFactory httpClientFactory,
    StudioSettingsService settingsService,
    ILogger<OllamaAgentExecutor> logger) : IAgentExecutor
{
    public string Name => "ollama";
    public string DisplayName => "Ollama";
    public IReadOnlyList<ExecutorSkill> AvailableSkills => [];

    // -------------------------------------------------------------------------
    // Health check — reuses the same /api/tags probe as the old OllamaHealthService
    // -------------------------------------------------------------------------

    public async Task<ExecutorHealthResult> CheckHealthAsync(CancellationToken ct = default)
    {
        var baseUrl = settingsService.GetSettings().OllamaBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/api/tags";

        try
        {
            var http = httpClientFactory.CreateClient("ollama-health");
            var response = await http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return new ExecutorHealthResult(false, $"Ollama returned HTTP {(int)response.StatusCode}");

            List<string> models = [];
            try
            {
                var doc = await response.Content.ReadFromJsonAsync<JsonDocument>(ct).ConfigureAwait(false);
                models = doc?.RootElement.GetProperty("models")
                    .EnumerateArray()
                    .Select(m => m.GetProperty("name").GetString() ?? string.Empty)
                    .Where(n => n.Length > 0)
                    .ToList() ?? [];
            }
            catch { /* model list is best-effort */ }

            var message = models.Count > 0
                ? $"{models.Count} model(s) available"
                : "Connected (no models found)";

            return new ExecutorHealthResult(true, message, models);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[ollama-health] Probe failed: {Message}", ex.Message);
            return new ExecutorHealthResult(false, $"Connection refused: {ex.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // Run
    // -------------------------------------------------------------------------

    public async Task<ExecutorResult> RunAsync(ExecutorContext context, ChannelWriter<string> output)
    {
        var systemPrompt = BuildSystemPrompt(context.WorkingDir);
        var rawBaseUrl = settingsService.GetSettings().OllamaBaseUrl.TrimEnd('/');

        if (!Uri.TryCreate(rawBaseUrl, UriKind.Absolute, out var parsedUri)
            || (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps))
        {
            var msg = $"OllamaBaseUrl '{rawBaseUrl}' is not a valid http/https URL. Aborting.";
            logger.LogError("[ollama] {Message}", msg);
            output.TryWrite($"[{DateTime.UtcNow:o}] [error] {msg}");
            return new ExecutorResult(1, ErrorMessage: msg);
        }

        var requestUri = $"{rawBaseUrl}/api/chat";
        logger.LogInformation("[ollama] Starting session — model={Model} endpoint={Uri}", context.ModelId, requestUri);
        output.TryWrite($"[{DateTime.UtcNow:o}] [ollama] model={context.ModelId} endpoint={requestUri}");

        var requestBody = new
        {
            model = context.ModelId,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = context.Prompt },
            },
            stream = true,
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var http = httpClientFactory.CreateClient("ollama");

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync(requestUri, httpContent, context.CancellationToken).ConfigureAwait(false);
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
            var msg = $"Ollama returned HTTP {(int)response.StatusCode}: {body}";
            logger.LogError("[ollama] {Message}", msg);
            output.TryWrite($"[{DateTime.UtcNow:o}] [error] {msg}");
            return new ExecutorResult(1, ErrorMessage: msg);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(context.CancellationToken).ConfigureAwait(false);
        using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8);

        var responseBuilder = new System.Text.StringBuilder();
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

            if (chunk?.Message?.Content is { Length: > 0 } content)
                responseBuilder.Append(content);

            if (chunk?.Done == true)
            {
                var fullResponse = responseBuilder.ToString().Trim();
                if (fullResponse.Length > 0)
                {
                    foreach (var responseLine in fullResponse.Split('\n'))
                        output.TryWrite($"[{DateTime.UtcNow:o}] {responseLine}");
                }

                logger.LogInformation("[ollama] Session complete — {Chars} chars", fullResponse.Length);
                return new ExecutorResult(0);
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
        return new ExecutorResult(0);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

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

    private sealed class OllamaChatChunk
    {
        public OllamaMessage? Message { get; set; }
        public bool Done { get; set; }
    }

    private sealed class OllamaMessage
    {
        public string? Content { get; set; }
    }
}
