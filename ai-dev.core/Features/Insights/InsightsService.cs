using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace AiDev.Features.Insights;

/// <summary>
/// Generates AI-powered qualitative analysis (insights) for a completed agent session by
/// posting the transcript to the Anthropic Messages API and parsing the structured JSON response.
///
/// Insights are written alongside the transcript as <c>{date}.insights.json</c>.
/// Generation is opt-in via <see cref="StudioSettings.EnableInsights"/>.
/// </summary>
public class InsightsService(
    IHttpClientFactory httpClientFactory,
    StudioSettingsService settingsService,
    ILogger<InsightsService> logger)
{
    // Use a fast, inexpensive model for analysis — not the full agent model.
    private const string InsightsModel = "claude-haiku-4-5-20251001";
    private const int MaxTokens = 1024;

    private const string SystemPrompt = """
        You are an expert software-engineering coach analyzing an AI agent session transcript.
        Your job is to produce a concise, structured JSON analysis of the session.
        Respond with ONLY valid JSON — no markdown fences, no explanations outside the JSON object.
        The JSON must match this schema exactly:
        {
          "taskClassification": "<feature|bug|refactor|investigation|other>",
          "sessionSizeRating": "<small|medium|large>",
          "issues": [
            { "description": "<what went wrong or was slow>", "impact": "<high|medium|low>" }
          ],
          "knowledgeGaps": ["<topic or context the agent lacked>"],
          "improvedPromptSuggestion": "<rewritten prompt that would have made the session more efficient>"
        }
        Keep each issue description under 120 characters.
        knowledgeGaps may be an empty array if none are identified.
        """;

    /// <summary>
    /// Generates insights for the session whose transcript lives at <paramref name="transcriptPath"/>
    /// and writes the result to <paramref name="insightPath"/>.
    /// Silently returns null when insights are disabled or when the API key is absent.
    /// </summary>
    public async Task<InsightResult?> GenerateAndSaveAsync(
        string transcriptPath,
        string insightPath,
        CancellationToken ct = default)
    {
        var studioSettings = settingsService.GetSettings();

        if (!studioSettings.EnableInsights)
            return null;

        var apiKey = studioSettings.AnthropicApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("[insights] AnthropicApiKey not configured — skipping insights generation");
            return null;
        }

        string transcriptContent;
        try
        {
            transcriptContent = await File.ReadAllTextAsync(transcriptPath, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[insights] Could not read transcript at {Path}", transcriptPath);
            return null;
        }

        if (string.IsNullOrWhiteSpace(transcriptContent))
            return null;

        logger.LogInformation("[insights] Generating insights for transcript at {Path}", transcriptPath);

        try
        {
            var result = await CallAnthropicAsync(apiKey, transcriptContent, ct);
            if (result == null) return null;

            var json = JsonSerializer.Serialize(result, JsonDefaults.Write);
            await File.WriteAllTextAsync(insightPath, json, ct);
            logger.LogInformation("[insights] Insights written to {Path}", insightPath);
            return result;
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("[insights] Insights generation was cancelled");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[insights] Failed to generate or save insights for {Path}", transcriptPath);
            return null;
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<InsightResult?> CallAnthropicAsync(string apiKey, string transcriptContent, CancellationToken ct)
    {
        var userMessage = $"Please analyse the following agent session transcript:\n\n{transcriptContent}";

        var requestBody = new JsonObject
        {
            ["model"]      = InsightsModel,
            ["max_tokens"] = MaxTokens,
            ["system"]     = SystemPrompt,
            ["messages"]   = new JsonArray(
                new JsonObject { ["role"] = "user", ["content"] = userMessage }
            ),
        }.ToJsonString();

        using var http = httpClientFactory.CreateClient("anthropic-insights");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "[insights] Anthropic API returned HTTP {Status}: {Body}",
                (int)response.StatusCode, body);
            return null;
        }

        return ParseResponse(body);
    }

    private InsightResult? ParseResponse(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var textContent = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? string.Empty;

            using var inner = JsonDocument.Parse(textContent);
            var root = inner.RootElement;

            var classification = root.TryGetProperty("taskClassification", out var tc)
                ? tc.GetString() ?? "other" : "other";

            var sizeRating = root.TryGetProperty("sessionSizeRating", out var sr)
                ? sr.GetString() ?? "medium" : "medium";

            List<InsightIssue> issues = [];
            if (root.TryGetProperty("issues", out var issuesEl) && issuesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in issuesEl.EnumerateArray())
                {
                    var desc   = item.TryGetProperty("description", out var d) ? d.GetString() ?? string.Empty : string.Empty;
                    var impact = item.TryGetProperty("impact",      out var i) ? i.GetString() ?? "medium"      : "medium";
                    if (!string.IsNullOrWhiteSpace(desc))
                        issues.Add(new(desc, impact));
                }
            }

            List<string> gaps = [];
            if (root.TryGetProperty("knowledgeGaps", out var gapsEl) && gapsEl.ValueKind == JsonValueKind.Array)
                gaps = gapsEl.EnumerateArray().Select(g => g.GetString() ?? string.Empty).Where(g => g.Length > 0).ToList();

            var suggestion = root.TryGetProperty("improvedPromptSuggestion", out var ips)
                ? ips.GetString() ?? string.Empty : string.Empty;

            return new InsightResult(classification, sizeRating, issues, gaps, suggestion);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[insights] Failed to parse Anthropic response as InsightResult");
            return null;
        }
    }
}
