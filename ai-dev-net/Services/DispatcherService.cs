namespace AiDevNet.Services;

/// <summary>
/// Background service that watches agent inboxes and the decisions/pending directory.
/// When new .md files arrive in an agent's inbox, it triggers AgentRunnerService.LaunchAgent.
///
/// Reliability model: dual-layer delivery guarantee.
///   1. FileSystemWatcher — fires immediately when a file is created/renamed.
///   2. Periodic poll (every 10 s) — catches anything FSW missed due to buffer overflow,
///      OS error, or race conditions. LaunchAgent is idempotent so double-firing is safe.
/// </summary>
public class DispatcherService(
    WorkspaceService workspace,
    AgentRunnerService runner,
    ILogger<DispatcherService> logger)
    : IHostedService, IDisposable
{
    private static readonly ActivitySource ActivitySource = new("AiDevNet.Dispatcher");

    // One watcher per watched directory
    private readonly List<FileSystemWatcher> _watchers = [];

    // Track which inbox dirs we're already watching (avoids duplicate watchers)
    private readonly HashSet<string> _watchedInboxDirs  = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _watchedAgentsDirs = new(StringComparer.OrdinalIgnoreCase);

    private Timer? _pollTimer;

    // -------------------------------------------------------------------------
    // IHostedService
    // -------------------------------------------------------------------------

    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("[dispatcher] Starting");

        var projects = workspace.ListProjects();
        foreach (var project in projects)
            WatchProject(project.Slug);

        var workspaceRoot = workspace.GetWorkspaceRoot();
        if (Directory.Exists(workspaceRoot))
            WatchForNewProjects(workspaceRoot);

        // Periodic poll — safety net for any FSW-missed events.
        // LaunchAgent is a no-op if the agent is already running, so this is safe.
        _pollTimer = new Timer(_ => PollAllProjects(), null,
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

        logger.LogInformation("[dispatcher] Watching {Count} project(s) with FSW + 10 s poll", projects.Count);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("[dispatcher] Stopping");
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _pollTimer?.Dispose();
        foreach (var w in _watchers) { try { w.Dispose(); } catch { } }
        _watchers.Clear();
        GC.SuppressFinalize(this);
    }

    // -------------------------------------------------------------------------
    // Per-project setup
    // -------------------------------------------------------------------------

    private void WatchProject(string projectSlug)
    {
        var projectDir = workspace.GetProjectPath(projectSlug);
        if (!Directory.Exists(projectDir)) return;

        var decisionsDir = Path.Combine(projectDir, "decisions", "pending");
        WatchDecisionsDir(projectSlug, decisionsDir);

        var agentsDir = Path.Combine(projectDir, "agents");
        if (Directory.Exists(agentsDir) && _watchedAgentsDirs.Add(agentsDir))
        {
            WatchForNewAgents(projectSlug, agentsDir);

            foreach (var agentDir in Directory.GetDirectories(agentsDir))
                WatchAgentInbox(projectSlug, Path.GetFileName(agentDir), agentDir);
        }

        ScanAndLaunchAgents(projectSlug, source: "startup");
    }

    // -------------------------------------------------------------------------
    // File system watchers
    // -------------------------------------------------------------------------

    private void WatchDecisionsDir(string projectSlug, string decisionsDir)
    {
        if (!Directory.Exists(decisionsDir)) return;

        var w = new FileSystemWatcher(decisionsDir, "*.md")
        {
            NotifyFilter          = NotifyFilters.FileName,
            IncludeSubdirectories = false,
            EnableRaisingEvents   = true,
        };
        w.Created += (_, e) =>
            logger.LogInformation("[dispatcher] New decision in {Project}: {File}",
                projectSlug, Path.GetFileName(e.FullPath));
        _watchers.Add(w);
    }

    private void WatchAgentInbox(string projectSlug, string agentSlug, string agentDir)
    {
        var inboxDir = Path.Combine(agentDir, "inbox");
        Directory.CreateDirectory(inboxDir);

        if (!_watchedInboxDirs.Add(inboxDir)) return;

        var w = new FileSystemWatcher(inboxDir, "*.md")
        {
            NotifyFilter       = NotifyFilters.FileName,
            IncludeSubdirectories = false,
            InternalBufferSize = 65536, // raised from 8192 default — overflows cause silent event loss
            EnableRaisingEvents = true,
        };

        w.Created += (_, e) => OnInboxMessage(projectSlug, agentSlug, e.FullPath, source: "fsw-created");
        // Renamed catches atomic writes (write-to-temp-then-rename) common on Windows
        w.Renamed += (_, e) => OnInboxMessage(projectSlug, agentSlug, e.FullPath, source: "fsw-renamed");
        w.Error   += (_, e) => OnWatcherError(w, projectSlug, agentSlug, e.GetException());

        _watchers.Add(w);
        logger.LogInformation("[dispatcher] Watching inbox: {InboxDir}", inboxDir);
    }

    private void OnWatcherError(FileSystemWatcher w, string projectSlug, string agentSlug, Exception ex)
    {
        logger.LogError(ex,
            "[dispatcher] FSW error for {Project}/{Agent} — restarting watcher and scanning inbox",
            projectSlug, agentSlug);

        // Re-enable the watcher so it resumes raising events
        try
        {
            w.EnableRaisingEvents = false;
            w.EnableRaisingEvents = true;
        }
        catch (Exception restartEx)
        {
            logger.LogError(restartEx, "[dispatcher] Failed to restart FSW for {Project}/{Agent}",
                projectSlug, agentSlug);
        }

        // Scan immediately to catch anything missed while the watcher was in error state
        ScanAndLaunchAgents(projectSlug, source: "fsw-error-recovery");
    }

    private void WatchForNewAgents(string projectSlug, string agentsDir)
    {
        var w = new FileSystemWatcher(agentsDir)
        {
            NotifyFilter          = NotifyFilters.DirectoryName,
            IncludeSubdirectories = false,
            EnableRaisingEvents   = true,
        };
        w.Created += (_, e) =>
        {
            var agentSlug = Path.GetFileName(e.FullPath);
            logger.LogInformation("[dispatcher] New agent detected: {Project}/{Agent}", projectSlug, agentSlug);
            // Brief delay so the agent folder structure is fully written before watching
            Task.Delay(500).ContinueWith(_ => WatchAgentInbox(projectSlug, agentSlug, e.FullPath));
        };
        _watchers.Add(w);
    }

    private void WatchForNewProjects(string workspaceRoot)
    {
        var w = new FileSystemWatcher(workspaceRoot)
        {
            NotifyFilter          = NotifyFilters.DirectoryName,
            IncludeSubdirectories = false,
            EnableRaisingEvents   = true,
        };
        w.Created += (_, e) =>
        {
            var projectSlug = Path.GetFileName(e.FullPath);
            Task.Delay(1000).ContinueWith(_ =>
            {
                if (File.Exists(Path.Combine(e.FullPath, "project.json")))
                {
                    logger.LogInformation("[dispatcher] New project detected: {Project}", projectSlug);
                    WatchProject(projectSlug);
                }
            });
        };
        _watchers.Add(w);
    }

    // -------------------------------------------------------------------------
    // Inbox event handling
    // -------------------------------------------------------------------------

    private void OnInboxMessage(string projectSlug, string agentSlug, string fullPath, string source)
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

        logger.LogInformation("[dispatcher] [{Source}] Inbox message for {Project}/{Agent}: {File}",
            source, projectSlug, agentSlug, fileName);

        if (runner.IsRunning(projectSlug, agentSlug))
        {
            logger.LogInformation(
                "[dispatcher] Agent {Project}/{Agent} already running — session will re-launch on exit if inbox is non-empty",
                projectSlug, agentSlug);
            activity?.SetTag("dispatch.outcome", "deferred-already-running");
            return;
        }

        var launched = runner.LaunchAgent(projectSlug, agentSlug);
        activity?.SetTag("dispatch.outcome", launched ? "launched" : "already-launched");
        logger.LogInformation("[dispatcher] {Outcome} {Project}/{Agent}",
            launched ? "Launched" : "Already running —", projectSlug, agentSlug);
    }

    // -------------------------------------------------------------------------
    // Polling scan (safety net)
    // -------------------------------------------------------------------------

    private void PollAllProjects()
    {
        foreach (var project in workspace.ListProjects())
            ScanAndLaunchAgents(project.Slug, source: "poll");
    }

    private void ScanAndLaunchAgents(string projectSlug, string source)
    {
        var agentsDir = Path.Combine(workspace.GetProjectPath(projectSlug), "agents");
        if (!Directory.Exists(agentsDir)) return;

        foreach (var agentDir in Directory.GetDirectories(agentsDir))
        {
            var agentSlug = Path.GetFileName(agentDir);
            var inboxDir  = Path.Combine(agentDir, "inbox");
            if (!Directory.Exists(inboxDir)) continue;

            var pending = Directory.GetFiles(inboxDir, "*.md")
                .Where(f => !f.Contains(
                    Path.DirectorySeparatorChar + "processed" + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (pending.Length == 0) continue;
            if (runner.IsRunning(projectSlug, agentSlug)) continue;

            logger.LogInformation(
                "[dispatcher] [{Source}] Found {Count} pending message(s) for {Project}/{Agent} — launching",
                source, pending.Length, projectSlug, agentSlug);

            runner.LaunchAgent(projectSlug, agentSlug);
        }
    }
}
