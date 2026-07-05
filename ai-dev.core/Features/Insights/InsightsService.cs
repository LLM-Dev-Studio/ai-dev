using AiDev.Executors;

using System.Text;

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
public partial class InsightsService(
    IEnumerable<IAgentExecutor> executors,
    StudioSettingsService settingsService,
    ILogger<InsightsService> logger)
{
    private readonly Dictionary<AgentExecutorName, IAgentExecutor> _executors =
        executors.GroupBy(e => e.Name).ToDictionary(g => g.Key, g => g.First());

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

        if (!AgentExecutorName.TryParse(studioSettings.InsightsExecutor, out var insightsExecutor)
            || !_executors.TryGetValue(insightsExecutor, out var executor))
        {
            LogExecutorNotRegistered(studioSettings.InsightsExecutor);
            return null;
        }

        var modelId = studioSettings.InsightsModel ?? executor.KnownModels.FirstOrDefault()?.Id;
        if (string.IsNullOrWhiteSpace(modelId))
        {
            LogNoModelConfigured(studioSettings.InsightsExecutor);
            return null;
        }

        string transcriptContent;
        try
        {
            transcriptContent = await File.ReadAllTextAsync(transcriptPath, ct);
        }
        catch (Exception ex)
        {
            LogCouldNotReadTranscript(ex, transcriptPath);
            return null;
        }

        if (string.IsNullOrWhiteSpace(transcriptContent))
            return null;

        // Insights uses a small system prompt (~50 tokens) and needs output room (~512 tokens).
        // The transcript is the only variable-size input. Cap it so the total fits a 4096-token
        // model — the smallest common local model context. Keeps the tail (most recent output).
        const int MaxTranscriptChars = 12_000; // ~3000 tokens, leaves headroom in a 4096 ctx
        var estimatedTokens = (transcriptContent.Length + 3) / 4;
        if (transcriptContent.Length > MaxTranscriptChars)
        {
            LogTranscriptTruncated(estimatedTokens, transcriptContent.Length, MaxTranscriptChars);
            transcriptContent = transcriptContent[^MaxTranscriptChars..];
        }
        else
        {
            LogTranscriptSize(estimatedTokens, transcriptContent.Length);
        }

        LogGeneratingInsights(executor.Name, modelId);

        // Create an isolated working directory so we can control the system prompt (CLAUDE.md)
        // without affecting any real agent. The directory structure mimics a workspace tree so
        // executors that rely on workspace-root plus project-slug context stay inside the temp directory.
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ai-insights-{Guid.NewGuid():N}");
        var workspaceRoot = new RootDir(Path.Combine(tempRoot, "workspaces"));
        var projectSlug = new ProjectSlug("insights");
        var workingDir = new AgentDir(Path.Combine(workspaceRoot.Value, "insights", "agents", "insights"));

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
                WorkspaceRoot: workspaceRoot,
                ProjectSlug: projectSlug,
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
                LogNoJsonInOutput();
                return null;
            }

            var result = ParseInsightResult(json);
            if (result == null) return null;

            await File.WriteAllTextAsync(insightPath, JsonSerializer.Serialize(result, JsonDefaults.Write), ct);
            LogInsightsWritten(insightPath);
            return result;
        }
        catch (OperationCanceledException)
        {
            LogInsightsCancelled();
            return null;
        }
        catch (Exception ex)
        {
            LogFailedToGenerateInsights(ex, transcriptPath);
            return null;
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); }
            catch (Exception ex) { LogCouldNotCleanUpTempDir(ex, tempRoot); }
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
                ? TaskClassification.From(tc.GetString()) : TaskClassification.Other;

            var sizeRating = root.TryGetProperty("sessionSizeRating", out var sr)
                ? SessionSizeRating.From(sr.GetString()) : SessionSizeRating.Medium;

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
                gaps = [.. gapsEl.EnumerateArray().Select(g => g.GetString() ?? string.Empty).Where(g => g.Length > 0)];

            var suggestion = root.TryGetProperty("improvedPromptSuggestion", out var ips)
                ? ips.GetString() ?? string.Empty : string.Empty;

            return new InsightResult(classification, sizeRating, issues, gaps, suggestion);
        }
        catch (Exception ex)
        {
            LogFailedToParseInsightResult(ex);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "[insights] Executor '{Executor}' not registered — skipping insights generation")]
    private partial void LogExecutorNotRegistered(string executor);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[insights] No model configured and no known models for executor '{Executor}'")]
    private partial void LogNoModelConfigured(string executor);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[insights] Could not read transcript at {Path}")]
    private partial void LogCouldNotReadTranscript(Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[insights] Transcript is ~{Tokens} tokens ({Chars} chars) — truncating to last {Max} chars to fit model context")]
    private partial void LogTranscriptTruncated(int tokens, int chars, int max);

    [LoggerMessage(Level = LogLevel.Information, Message = "[insights] Transcript is ~{Tokens} tokens ({Chars} chars)")]
    private partial void LogTranscriptSize(int tokens, int chars);

    [LoggerMessage(Level = LogLevel.Information, Message = "[insights] Generating insights using {Executor}/{Model}")]
    private partial void LogGeneratingInsights(AgentExecutorName executor, string model);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[insights] No JSON found in executor output")]
    private partial void LogNoJsonInOutput();

    [LoggerMessage(Level = LogLevel.Information, Message = "[insights] Insights written to {Path}")]
    private partial void LogInsightsWritten(string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "[insights] Insights generation was cancelled")]
    private partial void LogInsightsCancelled();

    [LoggerMessage(Level = LogLevel.Warning, Message = "[insights] Failed to generate or save insights for {Path}")]
    private partial void LogFailedToGenerateInsights(Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[insights] Could not clean up temp dir {Dir}")]
    private partial void LogCouldNotCleanUpTempDir(Exception ex, string dir);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[insights] Failed to parse executor output as InsightResult")]
    private partial void LogFailedToParseInsightResult(Exception ex);
}
