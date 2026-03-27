namespace AiDevNet.Services;

public class MessageItem
{
    public string Filename { get; set; } = string.Empty;
    public string AgentSlug { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Priority { get; set; } = "normal";
    public string Re { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class MessagesService(WorkspaceService workspace)
{
    public List<MessageItem> ListMessages(string projectSlug, string? agentSlug = null)
    {
        var results = new List<MessageItem>();
        var projectDir = workspace.GetProjectPath(projectSlug);
        var agentsDir = Path.Combine(projectDir, "agents");

        if (!Directory.Exists(agentsDir)) return results;

        string[] agentDirs;
        if (agentSlug != null)
        {
            var canonicalAgentsDir = Path.GetFullPath(agentsDir);
            var resolvedAgentDir = Path.GetFullPath(Path.Combine(agentsDir, agentSlug));
            if (!resolvedAgentDir.StartsWith(canonicalAgentsDir + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                return results;
            agentDirs = [resolvedAgentDir];
        }
        else
        {
            agentDirs = Directory.GetDirectories(agentsDir);
        }

        foreach (var agentDir in agentDirs)
        {
            var slug = Path.GetFileName(agentDir);
            var inboxDir = Path.Combine(agentDir, "inbox");
            if (!Directory.Exists(inboxDir)) continue;

            foreach (var file in Directory.GetFiles(inboxDir, "*.md").OrderByDescending(f => f))
            {
                var item = ParseMessageFile(file, slug);
                if (item != null) results.Add(item);
            }
        }

        return results.OrderByDescending(m => m.Date).ToList();
    }

    private static MessageItem? ParseMessageFile(string path, string agentSlug)
    {
        try
        {
            var content = File.ReadAllText(path);
            var (fields, body) = FrontmatterParser.Parse(content);
            return new()
            {
                Filename = Path.GetFileName(path),
                AgentSlug = agentSlug,
                From = fields.GetValueOrDefault("from", string.Empty),
                To = fields.GetValueOrDefault("to", string.Empty),
                Date = fields.GetValueOrDefault("date", string.Empty),
                Priority = fields.GetValueOrDefault("priority", "normal"),
                Re = fields.GetValueOrDefault("re", string.Empty),
                Type = fields.GetValueOrDefault("type", string.Empty),
                Body = body.Trim(),
            };
        }
        catch { return null; }
    }
}
