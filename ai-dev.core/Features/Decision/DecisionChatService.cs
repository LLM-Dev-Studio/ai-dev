using AiDev.Features.Agent;

namespace AiDev.Features.Decision;

/// <summary>
/// Manages interactive chat between a human and an agent for a pending decision.
/// Chat history is persisted as append-only JSONL at decisions/chats/{decisionId}.jsonl.
/// Human messages are routed into the agent's inbox; agent replies arrive via outbox.
/// </summary>
public partial class DecisionChatService(
    WorkspacePaths paths,
    IAgentRunnerService runner,
    AgentInboxService inbox,
    ProjectStateChangedNotifier projectStateNotifier,
    ILogger<DecisionChatService> logger)
{
    // -------------------------------------------------------------------------
    // Read
    // -------------------------------------------------------------------------

    public IReadOnlyList<DecisionChatMessage> GetMessages(ProjectSlug projectSlug, DecisionId decisionId)
    {
        var chatPath = ChatPath(projectSlug, decisionId);
        if (!File.Exists(chatPath)) return [];

        try
        {
            var json = File.ReadAllText(chatPath);
            return ParseMessages(json, decisionId);
        }
        catch (IOException ex)
        {
            LogReadChatFailed(ex, decisionId);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogReadChatUnauthorized(ex, decisionId);
        }

        return [];
    }

    // -------------------------------------------------------------------------
    // Write (human → agent)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sends a human message: appends to JSONL, writes to agent's inbox, auto-launches agent.
    /// </summary>
    public string? SendHumanMessage(ProjectSlug projectSlug, DecisionId decisionId, AgentSlug agentSlug, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return "Message cannot be empty.";

        var msg = new DecisionChatMessage(
            Id: Guid.NewGuid().ToString("N")[..8],
            DecisionId: decisionId,
            From: "human",
            IsHuman: true,
            Content: content.Trim(),
            Timestamp: DateTime.UtcNow);

        var appendError = AppendMessage(projectSlug, decisionId, msg);
        if (appendError != null) return appendError;

        // Write to agent inbox with decision-id header so agent knows which decision to reply to.
        var body = $"The human has replied to your decision request (decision-id: {decisionId}):\n\n{content.Trim()}\n\n" +
                   $"Please respond via write_outbox with type: decision-reply and decision-id: {decisionId}.";

        var inboxResult = inbox.WriteInboxMessage(
            projectSlug, agentSlug,
            from: "human",
            re: $"Re: decision {decisionId}",
            type: "decision-chat",
            priority: Priority.High,
            body: body,
            decisionId: decisionId);

        if (inboxResult is Err<Unit> inboxErr)
        {
            LogInboxWriteFailed(decisionId, inboxErr.Error.Message);
            return $"Failed to deliver message to agent: {inboxErr.Error.Message}";
        }

        // Auto-launch the agent so it processes the message.
        runner.LaunchAgent(projectSlug, agentSlug);

        return null;
    }

    // -------------------------------------------------------------------------
    // Flush agent replies from outbox
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scans the agent's outbox for decision-reply messages matching this decision,
    /// appends them to the JSONL, archives the outbox files, and fires the notifier.
    /// </summary>
    public bool FlushAgentReplies(ProjectSlug projectSlug, DecisionId decisionId, AgentSlug agentSlug)
    {
        var outboxDir = paths.AgentOutboxDir(projectSlug, agentSlug);
        if (!Directory.Exists(outboxDir)) return false;

        // Use a per-decision claiming folder so concurrent pollers can't double-process files.
        var claimingDir = Path.Combine(outboxDir, "claiming");
        Directory.CreateDirectory(claimingDir);

        var files = Directory.GetFiles(outboxDir, "*.md");
        var flushed = false;

        foreach (var file in files)
        {
            string? claimedPath = null;
            try
            {
                var text = File.ReadAllText(file);
                var (fields, body) = FrontmatterParser.Parse(text);

                if (!fields.TryGetValue("type", out var type) || type != "decision-reply") continue;
                if (!fields.TryGetValue("decision-id", out var msgDecisionId) || msgDecisionId != decisionId.Value) continue;

                // Atomically claim the file by moving it to the claiming folder.
                // If another poller already claimed it, File.Move throws and we skip.
                claimedPath = Path.Combine(claimingDir, Path.GetFileName(file));
                try { File.Move(file, claimedPath); }
                catch { continue; } // another poller claimed it first

                var from = fields.TryGetValue("from", out var f) ? f : agentSlug.Value;
                var timestamp = fields.TryGetValue("date", out var d)
                    && DateTime.TryParse(d, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : DateTime.UtcNow;

                var msg = new DecisionChatMessage(
                    Id: Guid.NewGuid().ToString("N")[..8],
                    DecisionId: decisionId,
                    From: from,
                    IsHuman: false,
                    Content: body.Trim(),
                    Timestamp: timestamp);

                var appendError = AppendMessage(projectSlug, decisionId, msg);
                if (appendError != null)
                {
                    LogAppendAgentReplyFailed(appendError);
                    // Move back to outbox so it can be retried.
                    try { File.Move(claimedPath, file); } catch { /* best-effort */ }
                    claimedPath = null;
                    continue;
                }

                // Archive the claimed file to processed/.
                var processedDir = Path.Combine(outboxDir, "processed");
                Directory.CreateDirectory(processedDir);
                File.Move(claimedPath, Path.Combine(processedDir, Path.GetFileName(claimedPath)), overwrite: true);
                claimedPath = null;
                flushed = true;
            }
            catch (Exception ex)
            {
                LogOutboxFileProcessingError(ex, file);
                // Return claimed file to outbox on unexpected error.
                if (claimedPath != null && File.Exists(claimedPath))
                    try { File.Move(claimedPath, file); } catch { /* best-effort */ }
            }
        }

        if (flushed) projectStateNotifier.Notify(projectSlug, ProjectStateChangeKind.Decisions);

        return flushed;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private string? AppendMessage(ProjectSlug projectSlug, DecisionId decisionId, DecisionChatMessage msg)
    {
        try
        {
            var chatPath = ChatPath(projectSlug, decisionId);
            Directory.CreateDirectory(Path.GetDirectoryName(chatPath)!);
            var line = System.Text.Json.JsonSerializer.Serialize(msg, JsonDefaults.WriteCompact);
            File.AppendAllText(chatPath, line + "\n");
            return null;
        }
        catch (Exception ex)
        {
            LogAppendMessageFailed(ex, decisionId);
            return ex.Message;
        }
    }

    private string ChatPath(ProjectSlug projectSlug, DecisionId decisionId) =>
        Path.Combine(paths.DecisionChatsDir(projectSlug), $"{decisionId}.jsonl");

    private IReadOnlyList<DecisionChatMessage> ParseMessages(string json, DecisionId decisionId)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        var messages = new List<DecisionChatMessage>();
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions
        {
            AllowMultipleValues = true,
        });

        while (true)
        {
            try
            {
                if (!reader.Read())
                    break;

                var message = System.Text.Json.JsonSerializer.Deserialize<DecisionChatMessage>(ref reader, JsonDefaults.Read);
                if (message != null)
                    messages.Add(message);
            }
            catch (JsonException ex)
            {
                LogParseChatHistoryFailed(ex, decisionId, messages.Count);
                break;
            }
        }

        return messages;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "[decision-chat] Failed to read chat for {DecisionId}")]
    private partial void LogReadChatFailed(Exception ex, DecisionId decisionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[decision-chat] Failed to read chat for {DecisionId}")]
    private partial void LogReadChatUnauthorized(Exception ex, DecisionId decisionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[decision-chat] Inbox write failed for {DecisionId}: {Error}")]
    private partial void LogInboxWriteFailed(DecisionId decisionId, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[decision-chat] Failed to append agent reply: {Error}")]
    private partial void LogAppendAgentReplyFailed(string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[decision-chat] Error processing outbox file {File}")]
    private partial void LogOutboxFileProcessingError(Exception ex, string file);

    [LoggerMessage(Level = LogLevel.Error, Message = "[decision-chat] Failed to append message to {DecisionId}")]
    private partial void LogAppendMessageFailed(Exception ex, DecisionId decisionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[decision-chat] Failed to parse chat history for {DecisionId}; returning {MessageCount} message(s)")]
    private partial void LogParseChatHistoryFailed(Exception ex, DecisionId decisionId, int messageCount);
}
