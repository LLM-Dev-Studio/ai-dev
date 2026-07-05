using AiDev.Executors;
using AiDev.Features.Agent;
using AiDev.Features.Board;
using AiDev.Features.Decision;
using AiDev.Features.Workspace;

namespace AiDev.Services;

/// <summary>
/// Monitors board tasks for stalls and ensures work keeps moving.
///
/// Every <see cref="ScanInterval"/>:
///   - Tasks in non-done columns that haven't moved in > <see cref="StallThreshold"/>
///     are considered stalled.
///   - Stalled tasks with an assignee receive an inbox nudge (subject to
///     <see cref="NudgeCooldown"/> to prevent spam). NudgedAt is reset when a task
///     moves columns so a progressing task is never counted as stalled.
///   - Stalled tasks with no assignee raise a decision for human intervention.
/// </summary>
public partial class OverwatchService(
    WorkspaceService workspace,
    IBoardService boardService,
    IAgentRunnerService runner,
    IAgentInboxService inbox,
    AgentService agentService,
    ExecutorHealthMonitor executorHealth,
    IDecisionsService decisionsService,
    ILogger<OverwatchService> logger)
    : IHostedService, IDisposable
{
    private static readonly ActivitySource ActivitySource = new("AiDevNet.Overwatch");

    private static readonly TimeSpan StallThreshold = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan NudgeCooldown = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(5);

    private CancellationTokenSource? _scanCts;
    private Task? _scanLoop;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LogStarting(StallThreshold.TotalMinutes, NudgeCooldown.TotalMinutes, ScanInterval.TotalMinutes);

        _scanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _scanLoop = RunScanLoopAsync(_scanCts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        LogStopping();
        _scanCts?.Cancel();
        if (_scanLoop != null)
        {
            try { await _scanLoop.WaitAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        Dispose();
    }

    public void Dispose()
    {
        var cts = Interlocked.Exchange(ref _scanCts, null);
        try { cts?.Cancel(); } catch (ObjectDisposedException) { }
        cts?.Dispose();
        GC.SuppressFinalize(this);
    }

    // -------------------------------------------------------------------------
    // Scan loop
    // -------------------------------------------------------------------------

    private async Task RunScanLoopAsync(CancellationToken cancellationToken)
    {
        // Initial delay — let the app finish initialising before the first scan.
        try { await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(ScanInterval);
        do
        {
            await ScanAllAsync(cancellationToken).ConfigureAwait(false);
        }
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
    }

    private async Task ScanAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var activity = ActivitySource.StartActivity("Overwatch.Scan", ActivityKind.Internal);
        var projects = workspace.ListProjects();
        activity?.SetTag("projects.count", projects.Count);

        foreach (var project in projects)
        {
            try { await ScanProjectAsync(project.Slug, activity?.Id, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LogScanProjectError(ex, project.Slug);
            }
        }
    }

    private Task ScanProjectAsync(ProjectSlug projectSlug, string? parentActivityId, CancellationToken cancellationToken)
    {
        return Task.Run(() => ScanProject(projectSlug, parentActivityId, cancellationToken), cancellationToken);
    }

    private void ScanProject(ProjectSlug projectSlug, string? parentActivityId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var activity = ActivitySource.StartActivity(
            "Overwatch.ScanProject", ActivityKind.Internal, parentActivityId);
        activity?.SetTag("project", projectSlug);

        var board = boardService.LoadBoard(projectSlug);
        if (board.Columns.Count == 0 || board.Tasks.Count == 0) return;

        var now = DateTime.UtcNow;
        var nudged = 0;
        var skipped = 0;
        var escalated = 0;

        // Build task → column lookup
        var taskColumn = board.Columns
            .SelectMany(c => c.TaskIds.Select(id => (id, col: c)))
            .ToDictionary(x => x.id, x => x.col);

        foreach (var (taskId, task) in board.Tasks)
        {
            if (!taskColumn.TryGetValue(taskId, out var column)) continue;

            // Skip completed tasks
                if (column.Id == ColumnId.Done || task.CompletedAt != null) continue;

            // How long in current column? Fall back to CreatedAt for legacy tasks without MovedAt.
            var sinceTime = task.MovedAt ?? task.CreatedAt;
            if (sinceTime == null) continue;

            var age = now - sinceTime.Value;
            if (age < StallThreshold) continue;

            // Respect nudge cooldown
            if (task.NudgedAt.HasValue && now - task.NudgedAt.Value < NudgeCooldown)
            {
                skipped++;
                continue;
            }

            using var taskActivity = ActivitySource.StartActivity(
                "Overwatch.StalledTask", ActivityKind.Internal, activity?.Id);
            taskActivity?.SetTag("project", projectSlug);
            taskActivity?.SetTag("task.id", taskId);
            taskActivity?.SetTag("task.title", task.Title);
            taskActivity?.SetTag("task.column", column.Title);
            taskActivity?.SetTag("task.priority", task.Priority);
            taskActivity?.SetTag("task.ageMinutes", (int)age.TotalMinutes);
            taskActivity?.SetTag("task.assignee", task.Assignee ?? "unassigned");

            LogTaskStalled((int)age.TotalMinutes, column.Title, task.Title, task.Assignee ?? "none");

            if (!string.IsNullOrEmpty(task.Assignee))
            {
                var outcome = NudgeAgent(projectSlug, task, column.Title, age);
                taskActivity?.SetTag("overwatch.action", outcome);
                if (outcome == "nudged") nudged++;
                else skipped++;
            }
            else
            {
                var outcome = RaiseDecision(projectSlug, task, column.Title, age);
                taskActivity?.SetTag("overwatch.action", outcome);
                if (outcome == "decision-raised") escalated++;
                else skipped++;
            }
        }

        if (nudged + escalated > 0)
            LogScanSummary(projectSlug, nudged, escalated, skipped);

        activity?.SetTag("overwatch.nudged", nudged);
        activity?.SetTag("overwatch.escalated", escalated);
        activity?.SetTag("overwatch.skipped", skipped);
    }

    // -------------------------------------------------------------------------
    // Actions
    // -------------------------------------------------------------------------

    private string NudgeAgent(ProjectSlug projectSlug, BoardTask task, string columnTitle, TimeSpan age)
    {
        if (!AgentSlug.TryParse(task.Assignee, out var assigneeSlug))
        {
            LogInvalidAssigneeSlug(task.Title, task.Assignee);
            return "invalid-assignee-slug";
        }

        // Don't nudge if the agent is already running — it'll pick up work when it next runs
        if (runner.IsRunning(projectSlug, assigneeSlug))
        {
            LogSkippingNudgeAgentRunning(assigneeSlug, task.Title);
            return "skip-agent-running";
        }

        // If the agent's executor is unhealthy, warn the human rather than nudging —
        // the agent would fail immediately on launch anyway.
        var agentInfo = agentService.LoadAgent(projectSlug, assigneeSlug);
        var executorName = agentInfo?.Executor ?? AgentExecutorName.Default;
        var health = executorHealth.GetHealth(executorName);
        if (!health.IsHealthy)
        {
            LogExecutorUnhealthy(executorName, assigneeSlug, task.Title);
            return RaiseExecutorOfflineDecision(projectSlug, task, assigneeSlug, executorName, health.Message);
        }

        var ageStr = FormatAge(age);
        var body = $"""
            Task "{task.Title}" has been stalled in **{columnTitle}** for {ageStr} with no progress.

            **Task ID:** {task.Id}
            **Priority:** {task.Priority}
            {(string.IsNullOrEmpty(task.Description) ? "" : $"**Description:** {task.Description}\n\n")}Please action this task and move it forward. If you are blocked, raise a decision or message the relevant agent explaining the blocker.
            """;

        var writeResult = inbox.WriteInboxMessage(
            projectSlug,
            assigneeSlug,
            from: "overwatch",
            re: $"Stalled task: {task.Title}",
            type: "overwatch-nudge",
            priority: task.Priority.IsUrgent ? task.Priority : Priority.High,
            body: body,
            taskId: task.Id);

        if (writeResult is Err<Unit> writeErr)
        {
            LogNudgeFailed(task.Assignee, task.Title, writeErr.Error.Message);
            return "nudge-failed";
        }

        LogNudged(task.Assignee, task.Title, ageStr);
        boardService.SetTaskNudged(projectSlug, task.Id);
        return "nudged";
    }

    private string RaiseDecision(ProjectSlug projectSlug, BoardTask task, string columnTitle, TimeSpan age)
    {
        var ageStr = FormatAge(age);
        var body = $"""
            Task "{task.Title}" has been in **{columnTitle}** for {ageStr} with no assigned agent.

            **Task ID:** {task.Id}
            **Priority:** {task.Priority}
            {(string.IsNullOrEmpty(task.Description) ? "" : $"**Description:** {task.Description}\n\n")}Please assign an agent to progress this task, or resolve it manually.
            """;

        var decisionResult = decisionsService.CreateDecision(
            projectSlug,
            from: "overwatch",
            subject: $"Unassigned stalled task: {task.Title}",
            priority: task.Priority.IsUrgent ? task.Priority : Priority.High,
            blocks: task.Id,
            body: body);

        if (decisionResult is Err<Unit> decisionErr)
        {
            LogRaiseDecisionFailed(task.Title, decisionErr.Error.Message);
            return "decision-failed";
        }

        LogDecisionRaised(task.Title, ageStr);
        boardService.SetTaskNudged(projectSlug, task.Id);
        return "decision-raised";
    }

    private string RaiseExecutorOfflineDecision(ProjectSlug projectSlug, BoardTask task,
        AgentSlug agentSlug, AgentExecutorName executorName, string healthMessage)
    {
        var body = $"""
            Agent **{agentSlug}** uses the **{executorName}** executor, which is currently unavailable.

            Task **"{task.Title}"** cannot be progressed until the executor is healthy again.

            **Task ID:** {task.Id}
            **Priority:** {task.Priority}
            **Executor:** {executorName}
            **Health status:** {healthMessage}

            Please investigate the executor, or reassign this task to an agent with a working executor.
            """;

        var decisionResult = decisionsService.CreateDecision(
            projectSlug,
            from: "overwatch",
            subject: $"Executor offline — {agentSlug} ({executorName}) cannot process \"{task.Title}\"",
            priority: task.Priority.IsUrgent ? task.Priority : Priority.High,
            blocks: task.Id,
            body: body);

        if (decisionResult is Err<Unit> decisionErr)
        {
            LogRaiseExecutorOfflineDecisionFailed(task.Title, decisionErr.Error.Message);
            return "decision-failed";
        }

        LogExecutorOfflineDecisionRaised(agentSlug, executorName, task.Title);
        boardService.SetTaskNudged(projectSlug, task.Id);
        return "decision-raised";
    }

    private static string FormatAge(TimeSpan age) =>
        age.TotalHours >= 1
            ? $"{(int)age.TotalHours}h {age.Minutes}m"
            : $"{(int)age.TotalMinutes}m";

    [LoggerMessage(Level = LogLevel.Information, Message = "[overwatch] Starting — stall: {Stall}m, cooldown: {Cooldown}m, interval: {Interval}m")]
    private partial void LogStarting(double stall, double cooldown, double interval);

    [LoggerMessage(Level = LogLevel.Information, Message = "[overwatch] Stopping")]
    private partial void LogStopping();

    [LoggerMessage(Level = LogLevel.Error, Message = "[overwatch] Error scanning project {Project}")]
    private partial void LogScanProjectError(Exception ex, ProjectSlug project);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[overwatch] Task stalled {Age}m in [{Column}]: \"{Title}\" — assignee: {Assignee}")]
    private partial void LogTaskStalled(int age, string column, string title, string assignee);

    [LoggerMessage(Level = LogLevel.Information, Message = "[overwatch] {Project}: {Nudged} nudged, {Escalated} escalated, {Skipped} in cooldown")]
    private partial void LogScanSummary(ProjectSlug project, int nudged, int escalated, int skipped);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[overwatch] Task \"{Title}\" has invalid assignee slug: {Assignee}")]
    private partial void LogInvalidAssigneeSlug(string title, string? assignee);

    [LoggerMessage(Level = LogLevel.Information, Message = "[overwatch] Agent {Agent} is currently running — skipping nudge for \"{Title}\"")]
    private partial void LogSkippingNudgeAgentRunning(AgentSlug agent, string title);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[overwatch] Executor '{Executor}' for agent {Agent} is unhealthy — raising decision for \"{Title}\"")]
    private partial void LogExecutorUnhealthy(AgentExecutorName executor, AgentSlug agent, string title);

    [LoggerMessage(Level = LogLevel.Error, Message = "[overwatch] Failed to nudge {Agent} for \"{Title}\": {Error}")]
    private partial void LogNudgeFailed(string? agent, string title, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "[overwatch] Nudged {Agent} for stalled task \"{Title}\" ({Age})")]
    private partial void LogNudged(string? agent, string title, string age);

    [LoggerMessage(Level = LogLevel.Error, Message = "[overwatch] Failed to raise decision for \"{Title}\": {Error}")]
    private partial void LogRaiseDecisionFailed(string title, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "[overwatch] Raised decision for unassigned stalled task \"{Title}\" ({Age})")]
    private partial void LogDecisionRaised(string title, string age);

    [LoggerMessage(Level = LogLevel.Error, Message = "[overwatch] Failed to raise executor offline decision for \"{Title}\": {Error}")]
    private partial void LogRaiseExecutorOfflineDecisionFailed(string title, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "[overwatch] Raised executor offline decision for agent {Agent} ({Executor}), task \"{Title}\"")]
    private partial void LogExecutorOfflineDecisionRaised(AgentSlug agent, AgentExecutorName executor, string title);
}
