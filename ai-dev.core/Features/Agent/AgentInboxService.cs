namespace AiDev.Features.Agent;

public class AgentInboxService(
    WorkspacePaths paths,
    ProjectStateChangedNotifier projectStateChangedNotifier,
    ILogger<AgentInboxService> logger)
{
    private static readonly ActivitySource ActivitySource = new("AiDevNet.AgentRunner");

    public Result<Unit> WriteInboxMessage(ProjectSlug projectSlug, AgentSlug agentSlug,
        MessageSource from, string re, MessageType type, Priority priority, string body,
        TaskId? taskId = null, DecisionId? decisionId = null)
    {
        using var activity = ActivitySource.StartActivity("Agent.WriteInboxMessage", ActivityKind.Internal);
        activity?.SetTag("agent.project", projectSlug);
        activity?.SetTag("agent.slug", agentSlug);
        activity?.SetTag("message.from", from);
        activity?.SetTag("message.type", type);
        activity?.SetTag("message.priority", priority.Value);

        var inboxDir = paths.AgentInboxDir(projectSlug, agentSlug);
        activity?.SetTag("message.inboxDir", inboxDir);

        try
        {
            Directory.CreateDirectory(inboxDir);
            var now = DateTime.UtcNow;
            var unique = $"{now:yyyyMMdd-HHmmss}-{now.Millisecond:D3}-{Guid.NewGuid().ToString("N")[..6]}-from-{from}.md";
            var filePath = Path.Combine(inboxDir, unique);
            var fields = new Dictionary<string, string>
            {
                ["from"] = from,
                ["to"] = agentSlug,
                ["date"] = now.ToString("o"),
                ["priority"] = priority.Value,
                ["re"] = re,
                ["type"] = type,
            };
            if (taskId != null) fields["task-id"] = taskId.ToString();
            if (decisionId != null) fields["decision-id"] = decisionId.Value;
            var content = FrontmatterParser.Stringify(fields, body);
            File.WriteAllText(filePath, content);
            projectStateChangedNotifier.Notify(projectSlug, ProjectStateChangeKind.Messages | ProjectStateChangeKind.Agents);
            activity?.SetTag("message.filename", unique);
            activity?.SetTag("message.success", true);
            logger.LogInformation("[runner] Inbox message written: {Project}/{Agent} ← {From} ({Type}) [{File}]",
                projectSlug, agentSlug, from, type, unique);
            return new Ok<Unit>(Unit.Value);
        }
        catch (Exception ex)
        {
            activity?.SetTag("message.success", false);
            activity?.SetTag("message.error", ex.Message);
            logger.LogError(ex, "[runner] Failed to write inbox message to {Project}/{Agent} from {From}: {Error}",
                projectSlug, agentSlug, from, ex.Message);
            return new Err<Unit>(new DomainError("INBOX_WRITE_FAILED", ex.Message));
        }
    }
}
