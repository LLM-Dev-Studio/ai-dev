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
    public bool IsProcessed { get; set; }
    public string TaskId { get; set; } = string.Empty;
}

public class MessagesService(WorkspacePaths paths)
{
    public List<MessageItem> ListMessages(ProjectSlug projectSlug, AgentSlug? agentSlug = null)
    {
        var results = new List<MessageItem>();
        var agentsDir = paths.AgentsDir(projectSlug);

        if (!Directory.Exists(agentsDir)) return results;

        string[] agentDirs;
        if (agentSlug != null)
        {
            agentDirs = [paths.AgentDir(projectSlug, agentSlug).Value];
        }
        else
        {
            agentDirs = Directory.GetDirectories(agentsDir);
        }

        foreach (var agentDir in agentDirs)
        {
            var dirName = Path.GetFileName(agentDir);
            if (!AgentSlug.TryParse(dirName, out var slug)) continue;

            var inboxDir = paths.AgentInboxDir(projectSlug, slug);
            var processedDir = paths.AgentInboxProcessedDir(projectSlug, slug);

            if (inboxDir.Exists())
            {
                results.AddRange(Directory.GetFiles(inboxDir.Value, "*.md")
                    .Select(file => ParseMessageFile(file, slug.Value, isProcessed: false))
                    .OfType<MessageItem>());
            }

            if (!processedDir.Exists())
            {
                continue;
            }

            results.AddRange(Directory.GetFiles(processedDir.Value, "*.md")
                .Select(file => ParseMessageFile(file, slug.Value, isProcessed: true))
                .OfType<MessageItem>());
        }

        return [.. results.OrderByDescending(m => m.Date)];
    }

    private static MessageItem? ParseMessageFile(string path, string agentSlug, bool isProcessed)
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
                IsProcessed = isProcessed,
                TaskId = fields.GetValueOrDefault("task-id", string.Empty),
            };
        }
        catch { return null; }
    }
}
