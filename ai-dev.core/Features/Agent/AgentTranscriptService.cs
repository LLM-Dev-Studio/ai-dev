using AiDev.Executors;

namespace AiDev.Features.Agent;

/// <summary>
/// Reads usage data persisted alongside agent transcripts.
/// </summary>
public class AgentTranscriptService(WorkspacePaths paths)
{
    /// <summary>
    /// Gets the most recent persisted session usage for an agent.
    /// </summary>
    /// <param name="projectSlug">The project that owns the agent.</param>
    /// <param name="agentSlug">The agent slug.</param>
    /// <returns>The most recent session usage, or <see langword="null"/> when unavailable.</returns>
    public TokenUsage? GetLastSessionUsage(ProjectSlug projectSlug, AgentSlug agentSlug)
    {
        var transcriptDir = paths.AgentTranscriptsDir(projectSlug, agentSlug);
        if (!Directory.Exists(transcriptDir)) return null;

        var usageFile = Directory.GetFiles(transcriptDir, "*.usage.json")
            .OrderByDescending(f => f)
            .FirstOrDefault();

        if (usageFile == null) return null;

        try
        {
            var json = File.ReadAllText(usageFile);
            return JsonSerializer.Deserialize<TokenUsage>(json, JsonDefaults.Read);
        }
        catch { return null; }
    }

    /// <summary>
    /// Gets persisted session usage for a specific transcript date.
    /// </summary>
    /// <param name="projectSlug">The project that owns the agent.</param>
    /// <param name="agentSlug">The agent slug.</param>
    /// <param name="date">The transcript date.</param>
    /// <returns>The session usage for the specified date, or <see langword="null"/> when unavailable.</returns>
    public TokenUsage? GetSessionUsage(ProjectSlug projectSlug, AgentSlug agentSlug, TranscriptDate date)
    {
        var transcriptDir = paths.AgentTranscriptsDir(projectSlug, agentSlug);
        var usagePath = Path.Combine(transcriptDir, $"{date.Value}.usage.json");
        if (!File.Exists(usagePath)) return null;
        try
        {
            var json = File.ReadAllText(usagePath);
            return JsonSerializer.Deserialize<TokenUsage>(json, JsonDefaults.Read);
        }
        catch { return null; }
    }
}
