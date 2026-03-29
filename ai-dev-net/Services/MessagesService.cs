namespace AiDevNet.Services;

public class MessagesService(WorkspacePaths paths)
{
    public List<MessageItem> ListMessages(ProjectSlug projectSlug, Models.AgentSlug? agentSlug = null)
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
            if (!Models.AgentSlug.TryParse(dirName, out var slug)) continue;

            var inboxDir = paths.AgentInboxDir(projectSlug, slug);
            var processedDir = paths.AgentInboxProcessedDir(projectSlug, slug);

            if (inboxDir.Exists())
            {
                results.AddRange(Directory.GetFiles(inboxDir.Value, "*.md")
                    .Select(file => ParseMessageFile(file, slug, isProcessed: false))
                    .OfType<MessageItem>());
            }

            if (!processedDir.Exists())
            {
                continue;
            }

            results.AddRange(Directory.GetFiles(processedDir.Value, "*.md")
                .Select(file => ParseMessageFile(file, slug, isProcessed: true))
                .OfType<MessageItem>());
        }

        return [.. results.OrderByDescending(m => m.Date)];
    }

    private static MessageItem? ParseMessageFile(string path, Models.AgentSlug agentSlug, bool isProcessed)
    {
        try
        {
            var content = File.ReadAllText(path);
            var (fields, body) = FrontmatterParser.Parse(content);
            var dateStr = fields.GetValueOrDefault("date");
            return new()
            {
                Filename = Path.GetFileName(path),
                AgentSlug = agentSlug,
                From = fields.GetValueOrDefault("from", string.Empty),
                To = fields.GetValueOrDefault("to", string.Empty),
                Date = DateTime.TryParse(dateStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : null,
                Priority = fields.GetValueOrDefault("priority", "normal"),
                Re = fields.GetValueOrDefault("re", string.Empty),
                Type = fields.GetValueOrDefault("type", string.Empty),
                Body = body.Trim(),
                IsProcessed = isProcessed,
                TaskId = TaskId.TryParse(fields.GetValueOrDefault("task-id"), out var tid) ? tid : null,
            };
        }
        catch { return null; }
    }
}
