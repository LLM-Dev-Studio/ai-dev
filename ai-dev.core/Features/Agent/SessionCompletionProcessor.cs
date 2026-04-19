using AiDev.Executors;
using AiDev.Features.Board;
using AiDev.Features.Insights;

namespace AiDev.Features.Agent;

/// <summary>
/// Handles all post-session cleanup: usage file accumulation, result.json processing,
/// inbox archival, and conditional relaunch.
/// </summary>
public class SessionCompletionProcessor(
    WorkspacePaths paths,
    BoardService boardService,
    InsightsService insightsService,
    ProjectStateChangedNotifier projectStateChangedNotifier,
    ILogger<SessionCompletionProcessor> logger)
{
    // Per-key semaphores to serialize usage file reads/writes when concurrent sessions finish same-day.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _usageLocks = new();

    public async Task ProcessAsync(
        string key,
        ProjectSlug projectSlug,
        AgentSlug agentSlug,
        string transcriptDir,
        TranscriptDate transcriptDate,
        string transcriptPath,
        string inboxDir,
        string[] inboxSnapshot,
        int exitCode,
        bool isRateLimited,
        bool preserveInbox,
        TokenUsage? sessionUsage,
        Func<ProjectSlug, AgentSlug, AgentLaunchTrigger?, bool> relaunch)
    {
        // Persist token usage alongside the transcript (accumulate across same-day sessions).
        if (sessionUsage != null)
        {
            try
            {
                var usagePath = Path.Combine(transcriptDir, $"{transcriptDate.Value}.usage.json");
                var usageLock = _usageLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
                if (await usageLock.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false))
                {
                    try
                    {
                        if (File.Exists(usagePath))
                        {
                            try
                            {
                                var existing = System.Text.Json.JsonSerializer.Deserialize<TokenUsage>(
                                    await File.ReadAllTextAsync(usagePath).ConfigureAwait(false), JsonDefaults.Read);
                                if (existing != null) sessionUsage = existing + sessionUsage;
                            }
                            catch { /* ignore corrupt existing file; overwrite with current session */ }
                        }
                        var usageJson = System.Text.Json.JsonSerializer.Serialize(sessionUsage, JsonDefaults.Write);
                        await File.WriteAllTextAsync(usagePath, usageJson).ConfigureAwait(false);
                        logger.LogInformation(
                            "[runner] Usage — {In} in / {Out} out tokens (daily total)",
                            sessionUsage.InputTokens, sessionUsage.OutputTokens);
                    }
                    finally { usageLock.Release(); }
                }
                else
                {
                    logger.LogWarning("[runner] Usage-lock timed out for {Key} — usage file not updated", key);
                }
            }
            catch (Exception ex) { logger.LogWarning(ex, "[runner] Failed to write usage file"); }
        }

        // Process result.json — read SessionResult from outbox, persist alongside transcript,
        // and automatically complete the associated board task.
        try
        {
            var outboxDir = paths.AgentOutboxDir(projectSlug, agentSlug).Value;
            var resultPath = Path.Combine(outboxDir, "result.json");
            if (File.Exists(resultPath))
            {
                var resultJson = await File.ReadAllTextAsync(resultPath).ConfigureAwait(false);
                var sessionResult = System.Text.Json.JsonSerializer.Deserialize<SessionResult>(resultJson, JsonDefaults.Read);
                if (sessionResult != null)
                {
                    var persistedResultPath = Path.Combine(transcriptDir, $"{transcriptDate.Value}.result.json");
                    await File.WriteAllTextAsync(persistedResultPath, resultJson).ConfigureAwait(false);
                    logger.LogInformation("[runner] Persisted result.json for {Key} → {Path}", key, persistedResultPath);

                    try { File.Delete(resultPath); }
                    catch (Exception delEx) { logger.LogWarning(delEx, "[runner] Failed to delete result.json from outbox for {Key}", key); }

                    // Prefer result.taskId; fall back to trigger task id if it was passed in the result.
                    if (!string.IsNullOrWhiteSpace(sessionResult.TaskId)
                        && TaskId.TryParse(sessionResult.TaskId, out var resultTaskId))
                    {
                        boardService.CompleteTaskFromResult(projectSlug, resultTaskId, sessionResult);
                        logger.LogInformation("[runner] Auto-completed board task {TaskId} from result.json for {Key}",
                            resultTaskId.Value, key);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[runner] Failed to process result.json for {Key}", key);
        }

        // Generate AI insights (fire-and-forget so insights finish even after session CT is cancelled).
        var insightPath = paths.InsightPath(projectSlug, agentSlug, transcriptDate).Value;
        _ = insightsService.GenerateAndSaveAsync(transcriptPath, insightPath, CancellationToken.None)
            .ContinueWith(t =>
            {
                if (t.Exception != null)
                    logger.LogWarning(t.Exception, "[runner] Insights generation faulted for {Project}/{Agent}",
                        projectSlug.Value, agentSlug.Value);
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

        // Inbox archival / relaunch.
        if (isRateLimited)
        {
            projectStateChangedNotifier.Notify(projectSlug, ProjectStateChangeKind.Messages);
        }
        else if (preserveInbox)
        {
            logger.LogWarning(
                "[runner] Agent {Key} ended with a recoverable configuration error — inbox preserved for retry", key);
            if (inboxSnapshot.Length > 0) projectStateChangedNotifier.Notify(projectSlug, ProjectStateChangeKind.Messages);
        }
        else
        {
            ArchiveInbox(inboxDir, inboxSnapshot);
            if (inboxSnapshot.Length > 0) projectStateChangedNotifier.Notify(projectSlug, ProjectStateChangeKind.Messages);

            var pendingAfterSession = Directory.Exists(inboxDir)
                ? Directory.GetFiles(inboxDir, "*.md")
                    .Count(f => !f.Contains(
                        Path.DirectorySeparatorChar + "processed" + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                : 0;

            if (pendingAfterSession > 0)
            {
                logger.LogInformation(
                    "[runner] {Count} message(s) arrived during session for {Key} — re-launching",
                    pendingAfterSession, key);
                relaunch(projectSlug, agentSlug, null);
            }
        }
    }

    private void ArchiveInbox(string inboxDir, string[] snapshot)
    {
        if (snapshot.Length == 0) return;
        var processedDir = Path.Combine(inboxDir, "processed");
        try
        {
            Directory.CreateDirectory(processedDir);
            foreach (var filename in snapshot)
            {
                var src = Path.Combine(inboxDir, filename);
                var dst = Path.Combine(processedDir, filename);
                if (File.Exists(src)) File.Move(src, dst, overwrite: true);
            }
            logger.LogInformation("[runner] Archived {Count} inbox file(s)", snapshot.Length);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[runner] Failed to archive inbox");
        }
    }
}
