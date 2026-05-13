using AiDev.Features.Planning;
using AiDev.Features.Planning.Models;
using AiDev.Models.Types;
using AiDev.Services;

using Microsoft.Extensions.Logging;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiDev.Executors;

/// <summary>
/// Anthropic Messages API implementation of <see cref="IPlanningLlmClient"/>.
/// Used when the analyst agent is configured with the "anthropic" executor.
/// </summary>
public sealed partial class AnthropicPlanningLlmClient(
    IHttpClientFactory httpClientFactory,
    StudioSettingsService settingsService,
    ILogger<AnthropicPlanningLlmClient> logger) : IPlanningLlmClient
{
    private const string AnthropicApiUrl = "https://api.anthropic.com/v1/messages";
    private const int MaxTokens = 8192;

    public AgentExecutorName ExecutorName => AgentExecutorName.Anthropic;

    public async Task<PlanningLlmResponse> ChatAsync(
        string modelId,
        string systemPrompt,
        IReadOnlyList<ConversationMessage> history,
        string userMessage,
        CancellationToken ct = default)
    {
        var apiKey = settingsService.GetSettings().AnthropicApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("AnthropicApiKey not configured in studio settings.");

        var messages = BuildMessages(history, userMessage);

        var requestBody = new JsonObject
        {
            ["model"] = modelId,
            ["max_tokens"] = MaxTokens,
            ["system"] = systemPrompt,
            ["messages"] = messages,
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
            LogApiCallFailed(ex);
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            LogHttpError((int)response.StatusCode, body);
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

        var inputTokens = 0;
        var outputTokens = 0;
        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("input_tokens", out var it)) inputTokens = it.GetInt32();
            if (usage.TryGetProperty("output_tokens", out var ot)) outputTokens = ot.GetInt32();
        }

        LogTokenUsage(inputTokens, outputTokens);
        return new PlanningLlmResponse(content, inputTokens, outputTokens);
    }

    // -------------------------------------------------------------------------
    // Logger messages
    // -------------------------------------------------------------------------

    [LoggerMessage(Level = LogLevel.Error, Message = "[planning-llm] Failed to call Anthropic API")]
    private partial void LogApiCallFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "[planning-llm] Anthropic returned HTTP {Status}: {Body}")]
    private partial void LogHttpError(int status, string body);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[planning-llm] tokens: {Input} in / {Output} out")]
    private partial void LogTokenUsage(int input, int output);

    private static JsonArray BuildMessages(IReadOnlyList<ConversationMessage> history, string newUserMessage)
    {
        var array = new JsonArray();
        foreach (var msg in history)
        {
            array.Add(new JsonObject
            {
                ["role"] = msg.Role,
                ["content"] = msg.Content,
            });
        }
        array.Add(new JsonObject
        {
            ["role"] = "user",
            ["content"] = newUserMessage,
        });
        return array;
    }
}
