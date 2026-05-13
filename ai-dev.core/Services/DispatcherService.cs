using AiDev.Features.Agent;
using AiDev.Features.Workspace;

namespace AiDev.Services;

/// <summary>
/// Background service that watches agent inboxes and the decisions/pending directory.
/// When new .md files arrive in an agent's inbox, it triggers AgentRunnerService.LaunchAgent.
///
/// Reliability model: dual-layer delivery guarantee.
///   1. FileSystemWatcher — fires immediately when a file is created/renamed.
///   2. Periodic poll (every 10 s) — catches anything FSW missed due to buffer overflow,
///      OS error, or race conditions. LaunchAgent is idempotent so double-firing is safe.
/// </summary>
public partial class DispatcherService(
    WorkspacePaths paths,
    WorkspaceService workspace,
    IAgentRunnerService runner,
    ProjectStateChangedNotifier projectStateNotifier,
    ILogger<DispatcherService> logger)
    : IHostedService, IDisposable
{
    private static readonly ActivitySource ActivitySource = new("AiDevNet.Dispatcher");

    // One watcher per watched directory
    private readonly List<FileSystemWatcher> _watchers = [];

    // Track which inbox dirs we're already watching (avoids duplicate watchers)
    private readonly HashSet<string> _watchedInboxDirs = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _watchedAgentsDirs = new(StringComparer.OrdinalIgnoreCase);

    private Timer? _pollTimer;

    // -------------------------------------------------------------------------
    // IHostedService
    // -------------------------------------------------------------------------

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LogStarting();

        var projects = workspace.ListProjects();
        foreach (var project in projects)
            WatchProject(project.Slug);

        var workspaceRoot = paths.Root;
        if (Directory.Exists(workspaceRoot))
            WatchForNewProjects(workspaceRoot);

        // Reset any agent.json files left in status=running from a previous crash/kill.
        await runner.RecoverStaleSessionsAsync(projects.Select(p => p.Slug));

        // Periodic poll — safety net for any FSW-missed events.
        // LaunchAgent is a no-op if the agent is already running, so this is safe.
        _pollTimer = new Timer(_ => PollAllProjects(CancellationToken.None), null,
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

        LogWatchingProjects(projects.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        LogStopping();
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _pollTimer?.Dispose();
        foreach (var w in _watchers)
        {
            try { w.Dispose(); }
            catch
            {
                // ignored
            }
        }
        _watchers.Clear();
        GC.SuppressFinalize(this);
    }

    // -------------------------------------------------------------------------
    // Per-project setup
    // -------------------------------------------------------------------------

    private void WatchProject(ProjectSlug projectSlug)
    {
        var projectDir = paths.ProjectDir(projectSlug);
        if (!Directory.Exists(projectDir)) return;

        var decisionsDir = paths.DecisionsPendingDir(projectSlug);
        WatchDecisionsDir(projectSlug, decisionsDir);

        var agentsDir = paths.AgentsDir(projectSlug);
        if (Directory.Exists(agentsDir) && _watchedAgentsDirs.Add(agentsDir))
        {
            WatchForNewAgents(projectSlug, agentsDir);

            foreach (var agentDir in Directory.GetDirectories(agentsDir))
            {
                if (AgentSlug.TryParse(Path.GetFileName(agentDir), out var agentSlug))
                    WatchAgentInbox(projectSlug, agentSlug);
            }
        }

        ScanAndLaunchAgents(projectSlug, source: "startup");
    }

    // -------------------------------------------------------------------------
    // File system watchers
    // -------------------------------------------------------------------------

    private void WatchDecisionsDir(ProjectSlug projectSlug, string decisionsDir)
    {
        if (!Directory.Exists(decisionsDir)) return;

        var w = new FileSystemWatcher(decisionsDir, "*.md")
        {
            NotifyFilter = NotifyFilters.FileName,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true,
        };
        w.Created += (_, e) =>
        {
            LogNewDecision(projectSlug, Path.GetFileName(e.FullPath));
            projectStateNotifier.Notify(projectSlug, ProjectStateChangeKind.Decisions);
        };
        w.Deleted += (_, e) =>
        {
            LogDecisionResolved(projectSlug, Path.GetFileName(e.FullPath));
            projectStateNotifier.Notify(projectSlug, ProjectStateChangeKind.Decisions);
        };
        _watchers.Add(w);
    }

    private void WatchAgentInbox(ProjectSlug projectSlug, AgentSlug agentSlug)
    {
        var inboxDir = paths.AgentInboxDir(projectSlug, agentSlug);
        Directory.CreateDirectory(inboxDir);

        if (!_watchedInboxDirs.Add(inboxDir)) return;

        var w = new FileSystemWatcher(inboxDir, "*.md")
        {
            NotifyFilter = NotifyFilters.FileName,
            IncludeSubdirectories = false,
            InternalBufferSize = 65536, // raised from 8192 default — overflows cause silent event loss
            EnableRaisingEvents = true,
        };

        w.Created += (_, e) => OnInboxMessage(projectSlug, agentSlug, e.FullPath, source: "fsw-created");
        // Renamed catches atomic writes (write-to-temp-then-rename) common on Windows
        w.Renamed += (_, e) => OnInboxMessage(projectSlug, agentSlug, e.FullPath, source: "fsw-renamed");
        w.Error += (_, e) => OnWatcherError(w, projectSlug, agentSlug, e.GetException());

        _watchers.Add(w);
        LogWatchingInbox(inboxDir);
    }

    private void OnWatcherError(FileSystemWatcher w, ProjectSlug projectSlug, AgentSlug agentSlug, Exception ex)
    {
        LogFswError(ex, projectSlug, agentSlug);

        // Re-enable the watcher so it resumes raising events
        try
        {
            w.EnableRaisingEvents = false;
            w.EnableRaisingEvents = true;
        }
        catch (Exception restartEx)
        {
            LogFswRestartFailed(restartEx, projectSlug, agentSlug);
        }

        // Scan immediately to catch anything missed while the watcher was in error state
        ScanAndLaunchAgents(projectSlug, source: "fsw-error-recovery");
    }

    private void WatchForNewAgents(ProjectSlug projectSlug, string agentsDir)
    {
        var w = new FileSystemWatcher(agentsDir)
        {
            NotifyFilter = NotifyFilters.DirectoryName,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true,
        };
        w.Created += (_, e) =>
        {
            if (!AgentSlug.TryParse(Path.GetFileName(e.FullPath), out var agentSlug)) return;
            LogNewAgentDetected(projectSlug, agentSlug);
            // Brief delay so the agent folder structure is fully written before watching
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(500).ConfigureAwait(false);
                    WatchAgentInbox(projectSlug, agentSlug);
                }
                catch (Exception ex)
                {
                    LogNewAgentWatcherFailed(ex, projectSlug, agentSlug);
                }
            });
        };
        _watchers.Add(w);
    }

    private void WatchForNewProjects(string workspaceRoot)
    {
        var w = new FileSystemWatcher(workspaceRoot)
        {
            NotifyFilter = NotifyFilters.DirectoryName,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true,
        };
        w.Created += (_, e) =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(1000).ConfigureAwait(false);
                    if (!File.Exists(Path.Combine(e.FullPath, "project.json"))) return;
                    if (!ProjectSlug.TryParse(Path.GetFileName(e.FullPath), out var projectSlug)) return;
                    LogNewProjectDetected(projectSlug);
                    WatchProject(projectSlug);
                }
                catch (Exception ex)
                {
                    LogNewProjectWatcherFailed(ex, e.FullPath);
                }
            });
        };
        _watchers.Add(w);
    }

    // -------------------------------------------------------------------------
    // Inbox event handling
    // -------------------------------------------------------------------------

    private void OnInboxMessage(ProjectSlug projectSlug, AgentSlug agentSlug, string fullPath, string source)
    {
        try
        {
            if (fullPath.Contains(Path.DirectorySeparatorChar + "processed" + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                return;

            if (!fullPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                return;

            var fileName = Path.GetFileName(fullPath);

            using var activity = ActivitySource.StartActivity("Dispatcher.InboxMessage", ActivityKind.Internal);
            activity?.SetTag("agent.project", projectSlug);
            activity?.SetTag("agent.slug", agentSlug);
            activity?.SetTag("message.file", fileName);
            activity?.SetTag("dispatch.source", source);

            LogInboxMessage(source, projectSlug, agentSlug, fileName);

            // Notify immediately so UIs refresh badges and agent inbox counts even when
            // this message is deferred because the agent is currently running.
            projectStateNotifier.Notify(projectSlug, ProjectStateChangeKind.Messages);

            if (runner.IsRunning(projectSlug, agentSlug))
            {
                LogAgentAlreadyRunning(projectSlug, agentSlug);
                activity?.SetTag("dispatch.outcome", "deferred-already-running");
                return;
            }

            var launched = runner.LaunchAgent(projectSlug, agentSlug, new AgentLaunchTrigger(
                Source: "dispatcher",
                Reason: source,
                ProjectSlug: projectSlug,
                MessageFile: fileName,
                ParentSpanId: activity?.Id));
            activity?.SetTag("dispatch.outcome", launched ? "launched" : "already-launched");
            LogLaunchOutcome(launched ? "Launched" : "Already running —", projectSlug, agentSlug);

            projectStateNotifier.Notify(projectSlug, ProjectStateChangeKind.Decisions);
        }
        catch (Exception ex)
        {
            LogOnInboxMessageFailed(ex, projectSlug, agentSlug, source, fullPath);
        }
    }

    // -------------------------------------------------------------------------
    // Polling scan (safety net)
    // -------------------------------------------------------------------------

    private void PollAllProjects(CancellationToken cancellationToken)
    {
        foreach (var project in workspace.ListProjects())
        {
            cancellationToken.ThrowIfCancellationRequested();
            ScanAndLaunchAgents(project.Slug, source: "poll");
        }
    }

    private void ScanAndLaunchAgents(ProjectSlug projectSlug, string source)
    {
        var agentsDir = paths.AgentsDir(projectSlug);
        if (!Directory.Exists(agentsDir)) return;

        foreach (var agentDir in Directory.GetDirectories(agentsDir))
        {
            if (!AgentSlug.TryParse(Path.GetFileName(agentDir), out var agentSlug)) continue;
            var inboxDir = paths.AgentInboxDir(projectSlug, agentSlug);
            if (!inboxDir.Exists()) continue;

            var pending = Directory.GetFiles(inboxDir.Value, "*.md")
                .Where(f => !f.Contains(
                    Path.DirectorySeparatorChar + "processed" + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (pending.Length == 0) continue;
            if (runner.IsRunning(projectSlug, agentSlug)) continue;

            LogLaunchingForPendingMessages(source, pending.Length, projectSlug, agentSlug);

            runner.LaunchAgent(projectSlug, agentSlug, new AgentLaunchTrigger(
                Source: "dispatcher",
                Reason: source,
                ProjectSlug: projectSlug));
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "[dispatcher] Starting")]
    private partial void LogStarting();

    [LoggerMessage(Level = LogLevel.Information, Message = "[dispatcher] Watching {Count} project(s) with FSW + 10 s poll")]
    private partial void LogWatchingProjects(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "[dispatcher] Stopping")]
    private partial void LogStopping();

    [LoggerMessage(Level = LogLevel.Information, Message = "[dispatcher] New decision in {Project}: {File}")]
    private partial void LogNewDecision(ProjectSlug project, string file);

    [LoggerMessage(Level = LogLevel.Information, Message = "[dispatcher] Decision resolved in {Project}: {File}")]
    private partial void LogDecisionResolved(ProjectSlug project, string file);

    [LoggerMessage(Level = LogLevel.Information, Message = "[dispatcher] Watching inbox: {InboxDir}")]
    private partial void LogWatchingInbox(string inboxDir);

    [LoggerMessage(Level = LogLevel.Error, Message = "[dispatcher] FSW error for {Project}/{Agent} — restarting watcher and scanning inbox")]
    private partial void LogFswError(Exception ex, ProjectSlug project, AgentSlug agent);

    [LoggerMessage(Level = LogLevel.Error, Message = "[dispatcher] Failed to restart FSW for {Project}/{Agent}")]
    private partial void LogFswRestartFailed(Exception ex, ProjectSlug project, AgentSlug agent);

    [LoggerMessage(Level = LogLevel.Information, Message = "[dispatcher] New agent detected: {Project}/{Agent}")]
    private partial void LogNewAgentDetected(ProjectSlug project, AgentSlug agent);

    [LoggerMessage(Level = LogLevel.Error, Message = "[dispatcher] Failed to set up inbox watcher for new agent {Project}/{Agent}")]
    private partial void LogNewAgentWatcherFailed(Exception ex, ProjectSlug project, AgentSlug agent);

    [LoggerMessage(Level = LogLevel.Information, Message = "[dispatcher] New project detected: {Project}")]
    private partial void LogNewProjectDetected(ProjectSlug project);

    [LoggerMessage(Level = LogLevel.Error, Message = "[dispatcher] Failed to set up watchers for new project at {Path}")]
    private partial void LogNewProjectWatcherFailed(Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "[dispatcher] [{Source}] Inbox message for {Project}/{Agent}: {File}")]
    private partial void LogInboxMessage(string source, ProjectSlug project, AgentSlug agent, string file);

    [LoggerMessage(Level = LogLevel.Information, Message = "[dispatcher] Agent {Project}/{Agent} already running — session will re-launch on exit if inbox is non-empty")]
    private partial void LogAgentAlreadyRunning(ProjectSlug project, AgentSlug agent);

    [LoggerMessage(Level = LogLevel.Information, Message = "[dispatcher] {Outcome} {Project}/{Agent}")]
    private partial void LogLaunchOutcome(string outcome, ProjectSlug project, AgentSlug agent);

    [LoggerMessage(Level = LogLevel.Error, Message = "[dispatcher] OnInboxMessage failed for {Project}/{Agent} ({Source}): {File}")]
    private partial void LogOnInboxMessageFailed(Exception ex, ProjectSlug project, AgentSlug agent, string source, string file);

    [LoggerMessage(Level = LogLevel.Information, Message = "[dispatcher] [{Source}] Found {Count} pending message(s) for {Project}/{Agent} — launching")]
    private partial void LogLaunchingForPendingMessages(string source, int count, ProjectSlug project, AgentSlug agent);
}
