using AiDev.Features.Agent;
using AiDev.Services;
using Microsoft.Extensions.Logging;

namespace AiDev.Features.Decision;

/// <summary>
/// Manages interactive chat between a human and an agent for a pending decision.
/// Chat history is persisted as append-only JSONL at decisions/chats/{decisionId}.jsonl.
/// Human messages are routed into the agent's inbox; agent replies arrive via outbox.
/// </summary>
public class DecisionChatService(
    WorkspacePaths paths,
    AgentRunnerService runner,
    DecisionChangedNotifier decisionNotifier,
    ILogger<DecisionChatService> logger)
{
    // -------------------------------------------------------------------------
    // Read
    // -------------------------------------------------------------------------

    public IReadOnlyList<DecisionChatMessage> GetMessages(ProjectSlug projectSlug, string decisionId)
    {
        var chatPath = ChatPath(projectSlug, decisionId);
        if (!File.Exists(chatPath)) return [];

        var messages = new List<DecisionChatMessage>();
        try
        {
            foreach (var line in File.ReadLines(chatPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var msg = System.Text.Json.JsonSerializer.Deserialize<DecisionChatMessage>(line, JsonDefaults.Read);
                if (msg != null) messages.Add(msg);
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "[decision-chat] Failed to read chat for {DecisionId}", decisionId); }

        return messages;
    }

    // -------------------------------------------------------------------------
    // Write (human → agent)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sends a human message: appends to JSONL, writes to agent's inbox, auto-launches agent.
    /// </summary>
    public string? SendHumanMessage(ProjectSlug projectSlug, string decisionId, string agentSlug, string content)
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

        var inboxError = runner.WriteInboxMessage(
            projectSlug, new(agentSlug),
            from: "human",
            re: $"Re: decision {decisionId}",
            type: "decision-chat",
            priority: "high",
            body: body,
            decisionId: decisionId);

        if (inboxError != null)
        {
            logger.LogWarning("[decision-chat] Inbox write failed for {DecisionId}: {Error}", decisionId, inboxError);
            return $"Failed to deliver message to agent: {inboxError}";
        }

        // Auto-launch the agent so it processes the message.
        runner.LaunchAgent(projectSlug, new(agentSlug));

        return null;
    }

    // -------------------------------------------------------------------------
    // Flush agent replies from outbox
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scans the agent's outbox for decision-reply messages matching this decision,
    /// appends them to the JSONL, archives the outbox files, and fires the notifier.
    /// </summary>
    public bool FlushAgentReplies(ProjectSlug projectSlug, string decisionId, string agentSlug)
    {
        var outboxDir = paths.AgentOutboxDir(projectSlug, new(agentSlug));
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
                if (!fields.TryGetValue("decision-id", out var msgDecisionId) || msgDecisionId != decisionId) continue;

                // Atomically claim the file by moving it to the claiming folder.
                // If another poller already claimed it, File.Move throws and we skip.
                claimedPath = Path.Combine(claimingDir, Path.GetFileName(file));
                try { File.Move(file, claimedPath); }
                catch { continue; } // another poller claimed it first

                var from = fields.TryGetValue("from", out var f) ? f : agentSlug;
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
                    logger.LogWarning("[decision-chat] Failed to append agent reply: {Error}", appendError);
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
                logger.LogWarning(ex, "[decision-chat] Error processing outbox file {File}", file);
                // Return claimed file to outbox on unexpected error.
                if (claimedPath != null && File.Exists(claimedPath))
                    try { File.Move(claimedPath, file); } catch { /* best-effort */ }
            }
        }

        if (flushed) decisionNotifier.Notify(projectSlug);

        return flushed;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private string? AppendMessage(ProjectSlug projectSlug, string decisionId, DecisionChatMessage msg)
    {
        try
        {
            var chatPath = ChatPath(projectSlug, decisionId);
            Directory.CreateDirectory(Path.GetDirectoryName(chatPath)!);
            var line = System.Text.Json.JsonSerializer.Serialize(msg, JsonDefaults.Write);
            File.AppendAllText(chatPath, line + "\n");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[decision-chat] Failed to append message to {DecisionId}", decisionId);
            return ex.Message;
        }
    }

    private string ChatPath(ProjectSlug projectSlug, string decisionId) =>
        Path.Combine(paths.DecisionChatsDir(projectSlug), $"{decisionId}.jsonl");
}
