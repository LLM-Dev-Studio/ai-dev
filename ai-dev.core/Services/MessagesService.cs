using AiDev.Models;

namespace AiDev.Services;

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

    public void MarkProcessed(ProjectSlug projectSlug, AgentSlug agentSlug, string filename)
    {
        var inboxDir = paths.AgentInboxDir(projectSlug, agentSlug);
        var processedDir = paths.AgentInboxProcessedDir(projectSlug, agentSlug);

        var sourcePath = Path.Combine(inboxDir.Value, filename);
        if (!File.Exists(sourcePath)) return;

        Directory.CreateDirectory(processedDir.Value);
        File.Move(sourcePath, Path.Combine(processedDir.Value, filename), overwrite: true);
    }

    private static MessageItem? ParseMessageFile(string path, AgentSlug agentSlug, bool isProcessed)
    {
        try
        {
            var content = File.ReadAllText(path);
            var (fields, body) = FrontmatterParser.Parse(content);
            var dateStr = fields.GetValueOrDefault("date");
            return new(
                filename: Path.GetFileName(path),
                agentSlug: agentSlug,
                from: fields.GetValueOrDefault("from", string.Empty),
                to: fields.GetValueOrDefault("to", string.Empty),
                re: fields.GetValueOrDefault("re", string.Empty),
                type: fields.GetValueOrDefault("type", string.Empty),
                body: body.Trim(),
                date: DateTime.TryParse(dateStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : null,
                priority: Priority.From(fields.GetValueOrDefault("priority", Priority.Normal.Value)),
                isProcessed: isProcessed,
                taskId: TaskId.TryParse(fields.GetValueOrDefault("task-id"), out var tid) ? tid : null,
                playbook: fields.TryGetValue("playbook", out var pb) ? pb : null);
        }
        catch { return null; }
    }
}
