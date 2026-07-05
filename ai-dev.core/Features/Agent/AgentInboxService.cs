namespace AiDev.Features.Agent;

/// <summary>
/// Writes inbox messages for agents and notifies project state changes.
/// </summary>
public partial class AgentInboxService(
    WorkspacePaths paths,
    ProjectStateChangedNotifier projectStateChangedNotifier,
    ILogger<AgentInboxService> logger) : IAgentInboxService
{
    private static readonly ActivitySource ActivitySource = new("AiDevNet.AgentRunner");

    /// <summary>
    /// Writes a message into an agent inbox.
    /// </summary>
    /// <param name="projectSlug">The project that owns the agent.</param>
    /// <param name="agentSlug">The target agent slug.</param>
    /// <param name="from">The message source.</param>
    /// <param name="re">The message subject.</param>
    /// <param name="type">The message type.</param>
    /// <param name="priority">The message priority.</param>
    /// <param name="body">The message body.</param>
    /// <param name="taskId">The optional associated task identifier.</param>
    /// <param name="decisionId">The optional associated decision identifier.</param>
    /// <returns>The result of the write operation.</returns>
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
            LogInboxMessageWritten(projectSlug, agentSlug, from, type, unique);
            return new Ok<Unit>(Unit.Value);
        }
        catch (Exception ex)
        {
            activity?.SetTag("message.success", false);
            activity?.SetTag("message.error", ex.Message);
            LogInboxWriteFailed(ex, projectSlug, agentSlug, from, ex.Message);
            return new Err<Unit>(new DomainError("INBOX_WRITE_FAILED", ex.Message));
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "[runner] Inbox message written: {Project}/{Agent} ← {From} ({Type}) [{File}]")]
    private partial void LogInboxMessageWritten(ProjectSlug project, AgentSlug agent, MessageSource from, MessageType type, string file);

    [LoggerMessage(Level = LogLevel.Error, Message = "[runner] Failed to write inbox message to {Project}/{Agent} from {From}: {Error}")]
    private partial void LogInboxWriteFailed(Exception ex, ProjectSlug project, AgentSlug agent, MessageSource from, string error);
}
