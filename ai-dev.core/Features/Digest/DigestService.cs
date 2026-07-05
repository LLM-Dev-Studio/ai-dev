using AiDev.Features.Agent;

namespace AiDev.Features.Digest;

/// <summary>
/// Builds daily digest summaries for a project.
/// </summary>
public class DigestService(WorkspacePaths paths, AgentService agentService)
{
    /// <summary>
    /// Gets the digest data for the specified project and date.
    /// </summary>
    /// <param name="projectSlug">The project slug to summarize.</param>
    /// <param name="date">The date to summarize.</param>
    /// <returns>The digest data for the requested project and date.</returns>
    public DigestData GetDigest(ProjectSlug projectSlug, DateOnly date)
    {
        var agentsDir = paths.AgentsDir(projectSlug);
        var pendingDir = paths.DecisionsPendingDir(projectSlug);
        var resolvedDir = paths.DecisionsResolvedDir(projectSlug);

        var pendingCount = Directory.Exists(pendingDir) ? Directory.GetFiles(pendingDir, "*.md").Length : 0;
        var resolvedCount = CountFilesForDate(resolvedDir, date);
        var agentActivity = new List<AgentActivityItem>();
        var totalMessages = 0;

        if (Directory.Exists(agentsDir))
        {
            foreach (var agentDir in Directory.GetDirectories(agentsDir))
            {
                if (!AgentSlug.TryParse(Path.GetFileName(agentDir), out var agentSlug)) continue;

                var agentInfo = agentService.LoadAgent(projectSlug, agentSlug);
                var name = agentInfo?.Name ?? agentSlug.Value;
                var model = agentInfo?.Model ?? string.Empty;

                var sent = CountFilesForDate(paths.AgentOutboxDir(projectSlug, agentSlug), date);
                var received = CountFilesForDate(paths.AgentInboxDir(projectSlug, agentSlug), date);
                totalMessages += received;

                agentActivity.Add(new()
                {
                    AgentSlug = agentSlug,
                    AgentName = name,
                    Executor = agentInfo?.Executor,
                    Model = model,
                    MessagesSent = sent,
                    MessagesReceived = received,
                });
            }
        }

        return new()
        {
            Date = date,
            TotalMessages = totalMessages,
            PendingDecisions = pendingCount,
            ResolvedDecisions = resolvedCount,
            AgentActivity = [.. agentActivity.OrderBy(a => a.AgentName)],
        };
    }

    private static int CountFilesForDate(DirPath dir, DateOnly date)
    {
        if (!dir.Exists()) return 0;
        var prefix = date.ToString("yyyyMMdd");
        return Directory.GetFiles(dir.Value, "*.md")
            .Count(f => Path.GetFileName(f).StartsWith(prefix));
    }
}
