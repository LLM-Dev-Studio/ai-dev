using AiDev.Executors;
using System.Text;
using System.Text.Json.Nodes;

namespace AiDev.Features.Insights;

/// <summary>
/// Generates AI-powered qualitative analysis (insights) for a completed agent session.
/// Uses whichever <see cref="IAgentExecutor"/> and model are configured via
/// <see cref="StudioSettings.InsightsExecutor"/> and <see cref="StudioSettings.InsightsModel"/>
/// — not tied to any specific provider.
///
/// A temporary working directory containing the insights system-prompt as CLAUDE.md is
/// created for the call and deleted on completion, so no workspace state is polluted.
///
/// Insights are written alongside the transcript as <c>{date}.insights.json</c>.
/// Generation is opt-in: set <c>InsightsExecutor</c> to enable.
/// </summary>
public class InsightsService(
    IEnumerable<IAgentExecutor> executors,
    StudioSettingsService settingsService,
    ILogger<InsightsService> logger)
{
    private readonly Dictionary<string, IAgentExecutor> _executors =
        executors.GroupBy(e => e.Name).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    private const string AnalysisInstructions = """
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
    /// Silently returns null when insights are not configured or on any error.
    /// </summary>
    public async Task<InsightResult?> GenerateAndSaveAsync(
        string transcriptPath,
        string insightPath,
        CancellationToken ct = default)
    {
        var studioSettings = settingsService.GetSettings();

        if (string.IsNullOrWhiteSpace(studioSettings.InsightsExecutor))
            return null;

        if (!_executors.TryGetValue(studioSettings.InsightsExecutor, out var executor))
        {
            logger.LogWarning("[insights] Executor '{Executor}' not registered — skipping insights generation",
                studioSettings.InsightsExecutor);
            return null;
        }

        var modelId = studioSettings.InsightsModel ?? executor.KnownModels.FirstOrDefault()?.Id;
        if (string.IsNullOrWhiteSpace(modelId))
        {
            logger.LogWarning("[insights] No model configured and no known models for executor '{Executor}'",
                studioSettings.InsightsExecutor);
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

        logger.LogInformation("[insights] Generating insights using {Executor}/{Model}", executor.Name, modelId);

        // Create an isolated working directory so we can control the system prompt (CLAUDE.md)
        // without affecting any real agent. The directory structure mimics a workspace tree so
        // path-traversal logic in executors (e.g. workspaceRoot = workingDir/../..) lands safely
        // inside the temp directory.
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ai-insights-{Guid.NewGuid():N}");
        var workingDir = Path.Combine(tempRoot, "workspaces", "_insights", "agents", "insights");

        try
        {
            Directory.CreateDirectory(workingDir);
            // Write insights instructions as CLAUDE.md — all executor types read this as the system prompt.
            await File.WriteAllTextAsync(Path.Combine(workingDir, "CLAUDE.md"), AnalysisInstructions, ct);

            var prompt = $"Analyze the following agent session transcript and return only the JSON as specified:\n\n{transcriptContent}";

            var outputLines = new List<string>();
            var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
            var consumer = Task.Run(async () =>
            {
                await foreach (var line in channel.Reader.ReadAllAsync())
                    outputLines.Add(line);
            });

            var context = new ExecutorContext(
                WorkingDir: workingDir,
                ModelId: modelId,
                Prompt: prompt,
                CancellationToken: ct,
                EnabledSkills: [],   // no workspace tools — pure text generation
                ReportPid: null);

            try
            {
                await executor.RunAsync(context, channel.Writer);
            }
            finally
            {
                channel.Writer.TryComplete();
                await consumer;
            }

            var json = ExtractJson(outputLines);
            if (string.IsNullOrWhiteSpace(json))
            {
                logger.LogWarning("[insights] No JSON found in executor output");
                return null;
            }

            var result = ParseInsightResult(json);
            if (result == null) return null;

            await File.WriteAllTextAsync(insightPath, JsonSerializer.Serialize(result, JsonDefaults.Write), ct);
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
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); }
            catch (Exception ex) { logger.LogDebug(ex, "[insights] Could not clean up temp dir {Dir}", tempRoot); }
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Extracts the first outermost JSON object from executor output lines.
    /// Each line has the format <c>[timestamp] content</c>; metadata lines
    /// (where content starts with <c>[</c>, <c>▶</c>, or <c>⟳</c>) are skipped.
    /// </summary>
    private static string ExtractJson(IEnumerable<string> lines)
    {
        var sb = new StringBuilder();
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r', '\n');
            if (string.IsNullOrEmpty(line)) continue;

            // Strip [timestamp] prefix.
            string content;
            if (line.StartsWith('['))
            {
                var closeTs = line.IndexOf(']');
                if (closeTs > 0 && closeTs + 2 <= line.Length)
                    content = line[(closeTs + 2)..];
                else
                    continue;
            }
            else
            {
                content = line;
            }

            // Skip metadata: executor diagnostics, errors, tool calls, progress markers.
            if (content.StartsWith('[') || content.StartsWith('▶') || content.StartsWith('⟳'))
                continue;

            sb.Append(content);
        }

        var text = sb.ToString();
        var jsonStart = text.IndexOf('{');
        var jsonEnd = text.LastIndexOf('}');
        if (jsonStart < 0 || jsonEnd <= jsonStart) return string.Empty;
        return text[jsonStart..(jsonEnd + 1)];
    }

    private InsightResult? ParseInsightResult(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

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
            logger.LogWarning(ex, "[insights] Failed to parse executor output as InsightResult");
            return null;
        }
    }
}
