using AiDev.Executors;
using AiDev.Features.Board;
using AiDev.Features.Insights;

namespace AiDev.Features.Agent;

/// <summary>
/// Handles all post-session cleanup: usage file accumulation, result.json processing,
/// inbox archival, and conditional relaunch.
/// </summary>
public partial class SessionCompletionProcessor(
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
                        LogUsageDailyTotal(sessionUsage.InputTokens, sessionUsage.OutputTokens);
                    }
                    finally { usageLock.Release(); }
                }
                else
                {
                    LogUsageLockTimeout(key);
                }
            }
            catch (Exception ex) { LogUsageWriteFailed(ex); }
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
                    LogResultPersisted(key, persistedResultPath);

                    try { File.Delete(resultPath); }
                    catch (Exception delEx) { LogResultDeleteFailed(delEx, key); }

                    // Prefer result.taskId; fall back to trigger task id if it was passed in the result.
                    if (!string.IsNullOrWhiteSpace(sessionResult.TaskId)
                        && TaskId.TryParse(sessionResult.TaskId, out var resultTaskId))
                    {
                        boardService.CompleteTaskFromResult(projectSlug, resultTaskId, sessionResult);
                        LogTaskAutoCompleted(resultTaskId.Value, key);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogResultProcessFailed(ex, key);
        }

        // Generate AI insights (fire-and-forget so insights finish even after session CT is cancelled).
        var insightPath = paths.InsightPath(projectSlug, agentSlug, transcriptDate).Value;
        _ = insightsService.GenerateAndSaveAsync(transcriptPath, insightPath, CancellationToken.None)
            .ContinueWith(t =>
            {
                if (t.Exception != null)
                    LogInsightsFaulted(t.Exception, projectSlug.Value, agentSlug.Value);
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

        // Inbox archival / relaunch.
        if (isRateLimited)
        {
            projectStateChangedNotifier.Notify(projectSlug, ProjectStateChangeKind.Messages);
        }
        else if (preserveInbox)
        {
            LogInboxPreservedForRetry(key);
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
                LogRelaunchingForPendingMessages(pendingAfterSession, key);
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
            LogInboxArchived(snapshot.Length);
        }
        catch (Exception ex)
        {
            LogInboxArchiveFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "[runner] Usage — {In} in / {Out} out tokens (daily total)")]
    private partial void LogUsageDailyTotal(long @in, long @out);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[runner] Usage-lock timed out for {Key} — usage file not updated")]
    private partial void LogUsageLockTimeout(string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[runner] Failed to write usage file")]
    private partial void LogUsageWriteFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "[runner] Persisted result.json for {Key} → {Path}")]
    private partial void LogResultPersisted(string key, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[runner] Failed to delete result.json from outbox for {Key}")]
    private partial void LogResultDeleteFailed(Exception ex, string key);

    [LoggerMessage(Level = LogLevel.Information, Message = "[runner] Auto-completed board task {TaskId} from result.json for {Key}")]
    private partial void LogTaskAutoCompleted(string taskId, string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[runner] Failed to process result.json for {Key}")]
    private partial void LogResultProcessFailed(Exception ex, string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[runner] Insights generation faulted for {Project}/{Agent}")]
    private partial void LogInsightsFaulted(Exception ex, string project, string agent);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[runner] Agent {Key} ended with a recoverable configuration error — inbox preserved for retry")]
    private partial void LogInboxPreservedForRetry(string key);

    [LoggerMessage(Level = LogLevel.Information, Message = "[runner] {Count} message(s) arrived during session for {Key} — re-launching")]
    private partial void LogRelaunchingForPendingMessages(int count, string key);

    [LoggerMessage(Level = LogLevel.Information, Message = "[runner] Archived {Count} inbox file(s)")]
    private partial void LogInboxArchived(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[runner] Failed to archive inbox")]
    private partial void LogInboxArchiveFailed(Exception ex);
}
