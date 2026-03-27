namespace AiDevNet.Services;

public class AgentActivityItem
{
    public string AgentSlug { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public int MessagesSent { get; set; }
    public int MessagesReceived { get; set; }
}

public class DigestData
{
    public string Date { get; set; } = string.Empty;
    public int TotalMessages { get; set; }
    public int PendingDecisions { get; set; }
    public int ResolvedDecisions { get; set; }
    public List<AgentActivityItem> AgentActivity { get; set; } = [];
}

public class DigestService(WorkspaceService workspace)
{
    public DigestData GetDigest(string projectSlug, string date)
    {
        var projectDir = workspace.GetProjectPath(projectSlug);
        var agentsDir = Path.Combine(projectDir, "agents");

        var pendingDir = Path.Combine(projectDir, "decisions", "pending");
        var resolvedDir = Path.Combine(projectDir, "decisions", "resolved");

        var pendingCount = Directory.Exists(pendingDir) ? Directory.GetFiles(pendingDir, "*.md").Length : 0;
        var resolvedCount = CountFilesForDate(resolvedDir, date);
        var agentActivity = new List<AgentActivityItem>();
        var totalMessages = 0;

        if (Directory.Exists(agentsDir))
        {
            foreach (var agentDir in Directory.GetDirectories(agentsDir))
            {
                var slug = Path.GetFileName(agentDir);
                var jsonPath = Path.Combine(agentDir, "agent.json");
                var name = slug;
                if (File.Exists(jsonPath))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(jsonPath));
                        if (doc.RootElement.TryGetProperty("name", out var n)) name = n.GetString() ?? slug;
                    }
                    catch { }
                }

                var sent = CountFilesForDate(Path.Combine(agentDir, "outbox"), date);
                var received = CountFilesForDate(Path.Combine(agentDir, "inbox"), date);
                totalMessages += received;

                agentActivity.Add(new()
                {
                    AgentSlug = slug,
                    AgentName = name,
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

    private static int CountFilesForDate(string dir, string date)
    {
        if (!Directory.Exists(dir)) return 0;
        // Files named YYYYMMDD-HHMMSS-*.md — date prefix is first 8 chars of the compact date
        var prefix = date.Replace("-", ""); // "2026-03-27" → "20260327"
        return Directory.GetFiles(dir, "*.md")
            .Count(f => Path.GetFileName(f).StartsWith(prefix));
    }
}
