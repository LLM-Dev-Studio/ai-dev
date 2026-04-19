using AiDev.Features.Agent;

namespace AiDev.Features.Digest;

public class DigestService(WorkspacePaths paths, AgentService agentService)
{
    public DigestData GetDigest(ProjectSlug projectSlug, string date)
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
                var executor = agentInfo?.Executor.Value ?? string.Empty;
                var model = agentInfo?.Model ?? string.Empty;

                var sent = CountFilesForDate(paths.AgentOutboxDir(projectSlug, agentSlug), date);
                var received = CountFilesForDate(paths.AgentInboxDir(projectSlug, agentSlug), date);
                totalMessages += received;

                agentActivity.Add(new()
                {
                    AgentSlug = agentSlug.Value,
                    AgentName = name,
                    Executor = executor,
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
            AgentActivity = agentActivity.OrderBy(a => a.AgentName).ToList(),
        };
    }

    private static int CountFilesForDate(DirPath dir, string date)
    {
        if (!dir.Exists()) return 0;
        // Files named YYYYMMDD-HHMMSS-*.md — date prefix is first 8 chars of the compact date
        var prefix = date.Replace("-", ""); // "2026-03-27" → "20260327"
        return Directory.GetFiles(dir.Value, "*.md")
            .Count(f => Path.GetFileName(f).StartsWith(prefix));
    }
}
