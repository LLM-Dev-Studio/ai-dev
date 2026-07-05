using AiDev.Executors;
using AiDev.Features.Decision;
using AiDev.Features.Secrets;

namespace AiDev.Features.Agent;

/// <summary>
/// Manages launching and stopping agent sessions.
/// Selects the appropriate IAgentExecutor, builds the ExecutorContext (prompt, skills, model),
/// streams output to a transcript file, and handles inbox archiving and rate-limit suppression.
/// </summary>
public partial class AgentRunnerService(
    WorkspacePaths paths,
    ModelResolver modelResolver,
    AgentStatusWriter statusWriter,
    IEnumerable<IAgentExecutor> executors,
    IModelRegistry modelRegistry,
    AgentService agentService,
    AgentPromptBuilder promptBuilder,
    SessionCompletionProcessor completionProcessor,
    SecretsService secretsService,
    IDecisionsService decisionsService,
    ILogger<AgentRunnerService> logger,
    ProjectStateChangedNotifier projectStateChangedNotifier,
    FeatureFlagsService featureFlagsService,
    IEnumerable<ILocalAgentHook> localHooks) : IAgentRunnerService
{
    private static readonly ActivitySource ActivitySource = new("AiDevNet.AgentRunner");
    private readonly Dictionary<AgentExecutorName, IAgentExecutor> _executors =
        executors.GroupBy(e => e.Name).ToDictionary(g => g.Key, g => g.First());
    private readonly ILocalAgentHook? _localHook = localHooks.FirstOrDefault();
    private const string AgentPrompt =
        "Read your inbox and action any messages. Follow your CLAUDE.md session protocol.";
    private const string ProjectScopedMcpPrompt =
        "Your assigned project slug is '{0}', your agent slug is '{1}', and the current UTC time is {2}. " +
        "Use {2} as sessionStartedAt when calling UpdateAgentStatus. " +
        "For every MCP workspace tool call, pass projectSlug='{0}'. " +
        "Wherever your CLAUDE.md instructions say '{{your-slug}}', substitute '{1}'.";

    private sealed class SessionInfo(CancellationTokenSource cts)
    {
        public required ProjectSlug ProjectSlug { get; init; }
        public required AgentSlug AgentSlug { get; init; }
        public required DateTime StartedAt { get; init; }
        public AgentLaunchTrigger? Trigger { get; init; }
        public int Pid { get; set; }
        public CancellationTokenSource Cts { get; } = cts;
    }

    private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();
    private readonly ConcurrentDictionary<string, DateTime> _rateLimitedUntil = new();

    private static string Key(ProjectSlug project, AgentSlug agent) => $"{project.Value}/{agent.Value}";

    // -------------------------------------------------------------------------
    // Public surface
    // -------------------------------------------------------------------------

    public bool IsRunning(ProjectSlug projectSlug, AgentSlug agentSlug) =>
        _sessions.ContainsKey(Key(projectSlug, agentSlug));

/// <summary>
/// Returns true if the specified agent is currently rate-limited, meaning it has recently hit a rate limit and should suppress launches until the cooldown expires. Rate-limited agents are not automatically re-launched when new inbox messages arrive, but they can be manually launched by the user and will check the rate limit again at that time.
/// </summary>
/// <param name="projectSlug">The slug of the project.</param>
/// <param name="agentSlug">The slug of the agent.</param>
/// <returns>True if the agent is rate-limited; otherwise, false.</returns>
    public bool IsRateLimited(ProjectSlug projectSlug, AgentSlug agentSlug) =>
        _rateLimitedUntil.TryGetValue(Key(projectSlug, agentSlug), out var until) && DateTime.UtcNow < until;

/// <summary>
/// Gets a list of currently running agent sessions, including their project slug, agent slug, process ID, and start time. This can be used to display active sessions in the UI or for monitoring purposes.
/// </summary>
/// <returns>A list of running sessions.</returns>
    public IReadOnlyList<RunningSession> GetRunningSessions() =>
        [.. _sessions.Values.Select(s => new RunningSession
        {
            ProjectSlug = s.ProjectSlug,
            AgentSlug = s.AgentSlug,
            Pid = s.Pid,
            StartedAt = s.StartedAt,
        })];

    /// <summary>
    /// Resets any agent.json still showing status="running" that has no live session in
    /// this process. Called at startup to recover from a previous crash or forced kill
    /// that prevented the finally block from completing.
    /// </summary>
    public async Task RecoverStaleSessionsAsync(IEnumerable<ProjectSlug> projects)
    {
        foreach (var project in projects)
        {
            var agentsDir = paths.AgentsDir(project);
            if (!Directory.Exists(agentsDir)) continue;

            foreach (var agentDir in Directory.GetDirectories(agentsDir))
            {
                if (!AgentSlug.TryParse(Path.GetFileName(agentDir), out var slug)) continue;

                try
                {
                    var info = agentService.LoadAgent(project, slug);
                    if (info is not AgentInfoRunning) continue;
                    if (_sessions.ContainsKey(Key(project, slug))) continue;

                    LogRecoveringStaleSession(project.Value, slug.Value);

                    await statusWriter.UpdateAsync(agentDir, new()
                    {
                        ["status"] = "idle",
                        ["pid"] = null,
                        ["sessionStartedAt"] = null,
                    });
                    projectStateChangedNotifier.Notify(project, ProjectStateChangeKind.Agents);
                }
                catch (Exception ex)
                {
                    LogStaleSessionRecoveryFailed(ex, project.Value, slug.Value);
                }
            }
        }
    }

    /// Launches an agent. Returns false if already running or rate-limited.
    /// The process runs in the background — this method returns quickly.
    /// </summary>
    public bool LaunchAgent(ProjectSlug projectSlug, AgentSlug agentSlug, AgentLaunchTrigger? trigger = null)
    {
        var key = Key(projectSlug, agentSlug);
        if (_sessions.ContainsKey(key))
        {
            LogAgentAlreadyRunning(key);
            return false;
        }

        if (_rateLimitedUntil.TryGetValue(key, out var until) && DateTime.UtcNow < until)
        {
            LogAgentRateLimitedSkippingLaunch(until, key);
            return false;
        }

        var startedAt = DateTime.UtcNow;
        var cts = new CancellationTokenSource();
        var info = new SessionInfo(cts)
        {
            ProjectSlug = projectSlug,
            AgentSlug = agentSlug,
            StartedAt = startedAt,
            Trigger = trigger,
        };

        if (!_sessions.TryAdd(key, info))
            return false; // race condition — another caller won

        projectStateChangedNotifier.Notify(projectSlug, ProjectStateChangeKind.Agents);

        using var activity = ActivitySource.StartActivity("Agent.Launch", ActivityKind.Server);
        activity?.SetTag("agent.project", projectSlug);
        activity?.SetTag("agent.slug", agentSlug);
        activity?.SetTag("agent.startedAt", startedAt.ToString("o"));
        ApplyTriggerTags(activity, trigger);
        _ = RunSessionAsync(key, info, startedAt, activity?.Id)
            .ContinueWith(t =>
            {
                var ex = t.Exception?.InnerException ?? t.Exception;
                LogRunSessionFaulted(ex, key);
                _sessions.TryRemove(key, out _);

                // Best-effort status update so the UI shows the error.
                var agentDir = paths.AgentDir(info.ProjectSlug, info.AgentSlug);
                _ = statusWriter.UpdateAsync(agentDir, new()
                {
                    ["status"] = "error",
                    ["lastError"] = $"Agent session faulted: {ex?.Message ?? "unknown error"}",
                    ["lastErrorAt"] = DateTime.UtcNow.ToString("o"),
                    ["pid"] = (object?)null,
                    ["sessionStartedAt"] = (object?)null,
                });
                projectStateChangedNotifier.Notify(info.ProjectSlug, ProjectStateChangeKind.Agents);
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        return true;
    }

    /// <summary>
    /// Signals a running agent to stop. If the session has no live process (orphaned),
    /// it is forcibly removed so the agent can be re-launched.
    /// </summary>
    public bool StopAgent(ProjectSlug projectSlug, AgentSlug agentSlug)
    {
        var key = Key(projectSlug, agentSlug);
        if (!_sessions.TryGetValue(key, out var info)) return false;
        LogStoppingAgent(key);
        info.Cts.Cancel();
        projectStateChangedNotifier.Notify(projectSlug, ProjectStateChangeKind.Agents);

        // If the session has no PID it likely faulted before launching a process.
        // Forcibly remove it so the UI transitions out of "running" state and the
        // agent can be re-launched.
        if (info.Pid == 0)
        {
            LogOrphanedSessionRemoved(key);
            _sessions.TryRemove(key, out _);

            var agentDir = paths.AgentDir(projectSlug, agentSlug);
            _ = statusWriter.UpdateAsync(agentDir, new()
            {
                ["status"] = "idle",
                ["pid"] = (object?)null,
                ["sessionStartedAt"] = (object?)null,
            });

            projectStateChangedNotifier.Notify(projectSlug, ProjectStateChangeKind.Agents);
        }

        return true;
    }

    private async Task RunSessionAsync(string key, SessionInfo info, DateTime startedAt, string? parentActivityId = null)
    {
        var projectSlug = info.ProjectSlug;
        var agentSlug = info.AgentSlug;

        using var activity = ActivitySource.StartActivity("Agent.RunSession", ActivityKind.Internal, parentActivityId);
        activity?.SetTag("agent.project", projectSlug);
        activity?.SetTag("agent.slug", agentSlug);
        activity?.SetTag("agent.sessionStartedAt", startedAt.ToString("o"));
        ApplyTriggerTags(activity, info.Trigger);

        var agentDir = paths.AgentDir(projectSlug, agentSlug);
        var inboxDir = paths.AgentInboxDir(projectSlug, agentSlug);

        // Declared here so the finally block (completionProcessor.ProcessAsync) can reference it
        // even if we abort before the snapshot is taken. The actual read happens immediately before
        // promptBuilder.Build() to minimise the window in which a newly-arrived message is missed.
        var inboxSnapshot = Array.Empty<string>();

        // Load agent config — fail fast on missing or malformed agent.json rather than
        // silently defaulting to a different executor/model.
        var loadedInfo = agentService.LoadAgent(projectSlug, agentSlug);
        if (loadedInfo == null)
        {
            LogAgentJsonMissingOrMalformed(key);
            await statusWriter.UpdateAsync(agentDir, new()
            {
                ["lastError"] = "Missing or malformed agent.json; cannot determine executor and model.",
                ["lastErrorAt"] = startedAt.ToString("o"),
                ["status"] = "error",
            });
            projectStateChangedNotifier.Notify(projectSlug, ProjectStateChangeKind.Agents);
            _sessions.TryRemove(key, out _);
            return;
        }

        var modelId = modelResolver.Resolve(loadedInfo.Model ?? string.Empty, loadedInfo.Executor);
        var executorName = loadedInfo.Executor;
        var agentSkills = (IReadOnlyList<string>)loadedInfo.Skills;
        var agentThinking = loadedInfo.ThinkingLevel;
        activity?.SetTag("agent.executor", executorName.Value);

        if (!_executors.TryGetValue(executorName, out var resolvedExecutor))
        {
            var available = string.Join(", ", _executors.Keys);
            LogExecutorNotRegistered(key, executorName.Value, available);
            await statusWriter.UpdateAsync(agentDir, new()
            {
                ["lastError"] = $"Executor '{executorName.Value}' is not registered. Available: {available}",
                ["lastErrorAt"] = startedAt.ToString("o"),
                ["status"] = "error",
            });
            projectStateChangedNotifier.Notify(projectSlug, ProjectStateChangeKind.Agents);
            _sessions.TryRemove(key, out _);
            return;
        }

        // Warn if the model is not known to the registry for this executor.
        // For dynamic executors (Ollama, GitHub Models) the registry may not have data until
        // health checks run, so this is advisory only — we do not block launch.
        if (modelRegistry.Find(executorName, modelId) == null)
        {
            LogModelNotRegistered(key, modelId, executorName.Value);
        }

        await statusWriter.UpdateAsync(agentDir, new()
        {
            ["status"] = "running",
            ["lastRunAt"] = startedAt.ToString("o"),
            ["sessionStartedAt"] = startedAt.ToString("o"),
        });
        projectStateChangedNotifier.Notify(projectSlug, ProjectStateChangeKind.Agents);

        var transcriptDir = paths.AgentTranscriptsDir(projectSlug, agentSlug);
        Directory.CreateDirectory(transcriptDir);
        var transcriptDate = TranscriptDate.From(startedAt);
        var transcriptPath = paths.TranscriptPath(projectSlug, agentSlug, transcriptDate).Value;

        var outputChannel = Channel.CreateUnbounded<string>(new() { SingleReader = true });
        var consumerTask = Task.Run(async () =>
        {
            await using var transcript = new StreamWriter(transcriptPath, append: true, System.Text.Encoding.UTF8);
            await transcript.WriteLineAsync();
            await transcript.WriteLineAsync($"## Session started at {startedAt:o}");
            await transcript.WriteLineAsync($"executor: {executorName.Value} · model: {modelId}");
            await transcript.WriteLineAsync();
            await transcript.FlushAsync();
            await foreach (var line in outputChannel.Reader.ReadAllAsync())
            {
                await transcript.WriteLineAsync(line);
                await transcript.FlushAsync();
            }
        });

        // Snapshot the inbox immediately before building the prompt so any message that
        // arrived during setup is included. Messages that arrive after this point will
        // trigger a relaunch via completionProcessor.
        if (Directory.Exists(inboxDir))
        {
            try { inboxSnapshot = [.. Directory.GetFiles(inboxDir, "*.md").Select(Path.GetFileName).OfType<string>().OrderBy(f => f)]; }
            catch (Exception ex) { LogReadInboxDirectoryFailed(ex, inboxDir); }
        }

        // Build prompt: inject KB context and playbook before the standard instruction.
        var effectivePrompt = promptBuilder.Build(
            projectSlug, agentSlug,
            string.Format(ProjectScopedMcpPrompt, projectSlug.Value, agentSlug.Value, startedAt.ToString("o")),
            AgentPrompt,
            inboxDir, inboxSnapshot);

        var exitCode = 0;
        var isRateLimited = false;
        var preserveInbox = false;
        var requiresHumanDecision = false;
        string? sessionError = null;
        TokenUsage? sessionUsage = null;

        // Load project secrets for environment injection — values are sensitive, never log them.
        var secrets = secretsService.LoadDecryptedSecrets(projectSlug);

        var context = new ExecutorContext(
            WorkspaceRoot: paths.Root,
            ProjectSlug: projectSlug,
            WorkingDir: agentDir,
            ModelId: modelId,
            Prompt: effectivePrompt,
            CancellationToken: info.Cts.Token,
            EnabledSkills: agentSkills,
            ReportPid: pid =>
            {
                info.Pid = pid;
                _ = statusWriter.UpdateAsync(agentDir, new() { ["pid"] = pid })
                    .ContinueWith(t => LogWritePidFailed(t.Exception, key),
                        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                LogLaunchedWithPid(key, pid);
                activity?.SetTag("agent.pid", pid);
                activity?.AddEvent(new("process.started"));
            },
            Trigger: info.Trigger,
            ThinkingLevel: agentThinking,
            Secrets: secrets.Count > 0 ? secrets : null,
            ReportWarning: warning =>
            {
                LogAgentWarning(key, warning);
                _ = statusWriter.UpdateAsync(agentDir, new() { ["lastWarning"] = warning });
            });

        try
        {
            var useLocalHook = _localHook is not null
                && featureFlagsService.GetFlags().LocalFunctionalityEnabled
                && _localHook.IsApplicable(resolvedExecutor.Name);

            if (useLocalHook)
            {
                var hookContext = new LocalAgentHookContext(
                    Goal: context.Prompt,
                    WorkingDir: context.WorkingDir,
                    ModelId: context.ModelId,
                    ExecutorName: resolvedExecutor.Name,
                    SessionId: Guid.NewGuid());

                var hookResult = await _localHook!.RunAsync(hookContext, outputChannel.Writer, info.Cts.Token);
                exitCode = hookResult.Succeeded ? 0 : 1;
                sessionError = hookResult.ErrorMessage;
            }
            else
            {
                var result = await resolvedExecutor.RunAsync(context, outputChannel.Writer);
                exitCode = result.ExitCode;
                isRateLimited = result.IsRateLimited;
                preserveInbox = result.PreserveInbox;
                sessionError = result.ErrorMessage;
                sessionUsage = result.Usage;
                requiresHumanDecision = result.RequiresHumanDecision;
            }

            activity?.SetTag("agent.exitCode", exitCode);
            activity?.SetTag("agent.rateLimited", isRateLimited);
            activity?.SetTag("agent.preserveInbox", preserveInbox);

            if (exitCode == 0)
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
                activity?.AddEvent(new("process.exited"));
            }
            else
            {
                sessionError = string.IsNullOrWhiteSpace(sessionError)
                    ? $"Agent exited with code {exitCode}."
                    : sessionError;

                LogAgentFailedWithExitCode(key, exitCode, sessionError);
                activity?.SetTag("agent.error", true);
                activity?.SetTag("agent.errorMessage", sessionError);
                activity?.SetStatus(ActivityStatusCode.Error, sessionError);
                activity?.AddEvent(new("process.error"));
            }
        }
        catch (OperationCanceledException)
        {
            exitCode = 130;
            LogAgentCancelled(key);
            activity?.SetTag("agent.cancelled", true);
            activity?.AddEvent(new("process.cancelled"));
        }
        catch (Exception ex)
        {
            exitCode = 1;
            sessionError = ex.Message;
            LogAgentError(ex, key);
            activity?.SetTag("agent.error", true);
            activity?.SetTag("agent.errorMessage", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new("process.error"));
            outputChannel.Writer.TryWrite($"[{DateTime.UtcNow:o}] [error] {ex.Message}");
        }
        finally
        {
            var exitedAt = DateTime.UtcNow;

            // Remove from live sessions immediately — this is the single most important
            // cleanup step. Do it first, before any I/O that could throw, so IsRunning()
            // returns false and the UI/poll timer reflect reality even if subsequent
            // cleanup steps fail.
            _sessions.TryRemove(key, out _);

            // Flush transcript writer — wrapped so a disk/stream error can't abort the
            // rest of cleanup (status write, inbox archival, relaunch check).
            outputChannel.Writer.TryWrite(string.Empty);
            outputChannel.Writer.TryWrite($"## Session ended at {exitedAt:o} (exit code: {exitCode})");
            outputChannel.Writer.TryComplete();
            try { await consumerTask; }
            catch (Exception ex) { LogTranscriptFlushFaulted(ex, key); }

            activity?.SetTag("agent.finishedAt", exitedAt.ToString("o"));
            activity?.AddEvent(new("session.finished"));
            LogAgentFinished(key, exitCode, exitedAt);

            // Write final status to agent.json — wrapped so a disk error can't abort
            // inbox archival or the relaunch check below.
            await statusWriter.UpdateAsync(agentDir, new()
            {
                ["status"] = exitCode is 0 or 130 ? "idle" : "error",
                ["pid"] = null,
                ["sessionStartedAt"] = null,
                ["lastError"] = exitCode == 0 || exitCode == 130 || string.IsNullOrWhiteSpace(sessionError) ? null : sessionError,
                ["lastErrorAt"] = exitCode == 0 || exitCode == 130 || string.IsNullOrWhiteSpace(sessionError) ? null : exitedAt.ToString("o"),
            });
            projectStateChangedNotifier.Notify(projectSlug, ProjectStateChangeKind.Agents);

            if (requiresHumanDecision && !string.IsNullOrWhiteSpace(sessionError))
            {
                var agent = agentService.LoadAgent(projectSlug, agentSlug);
                var agentLabel = agent?.Name ?? agentSlug.Value;
                var model = agent?.Model ?? "unknown model";
                decisionsService.CreateDecision(
                    projectSlug,
                    from: agentSlug.Value,
                    subject: $"Model too small for {agentLabel}",
                    priority: Priority.High,
                    blocks: agentSlug.Value,
                    body: $"Agent **{agentLabel}** could not run because `{model}` has a context window that is too small.\n\n{sessionError}\n\n**Action required:** In LM Studio, reload `{model}` with a larger context (≥ 8192 tokens), or select a different model for this agent in its settings.");
            }

            _sessions.TryRemove(key, out _);

            if (isRateLimited)
            {
                var suppressUntil = DateTime.UtcNow.AddMinutes(30);
                _rateLimitedUntil[key] = suppressUntil;
                LogAgentRateLimited(key, suppressUntil);
            }
            else
            {
                _rateLimitedUntil.TryRemove(key, out _);
            }

            await completionProcessor.ProcessAsync(
                key, projectSlug, agentSlug,
                transcriptDir, transcriptDate, transcriptPath,
                inboxDir, inboxSnapshot,
                exitCode, isRateLimited, preserveInbox,
                sessionUsage,
                relaunch: LaunchAgent);

            if (!isRateLimited && !preserveInbox)
                activity?.SetTag("agent.relaunchReason", "inbox-messages-during-session");
        }
    }

    private static void ApplyTriggerTags(Activity? activity, AgentLaunchTrigger? trigger)
    {
        if (activity == null || trigger == null)
            return;

        activity.SetTag("agent.trigger.source", trigger.Source);
        activity.SetTag("agent.trigger.reason", trigger.Reason);
        if (trigger.ProjectSlug is not null)
            activity.SetTag("project.slug", trigger.ProjectSlug.Value);
        if (trigger.TaskId is not null)
            activity.SetTag("task.id", trigger.TaskId.Value);
        if (trigger.DecisionId is not null)
            activity.SetTag("decision.id", trigger.DecisionId.Value);
        if (!string.IsNullOrWhiteSpace(trigger.MessageFile))
            activity.SetTag("message.file", trigger.MessageFile);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "[runner] Recovering stale running state for {Project}/{Agent} — resetting to idle")]
    private partial void LogRecoveringStaleSession(string project, string agent);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[runner] Failed to inspect agent {Project}/{Agent} during stale-session recovery")]
    private partial void LogStaleSessionRecoveryFailed(Exception ex, string project, string agent);

    [LoggerMessage(Level = LogLevel.Information, Message = "[runner] Agent already running: {Key}")]
    private partial void LogAgentAlreadyRunning(string key);

    [LoggerMessage(Level = LogLevel.Information, Message = "[runner] Agent rate-limited until {Until}, skipping launch: {Key}")]
    private partial void LogAgentRateLimitedSkippingLaunch(DateTime until, string key);

    [LoggerMessage(Level = LogLevel.Error, Message = "[runner] RunSessionAsync faulted for {Key} before session try-catch")]
    private partial void LogRunSessionFaulted(Exception? ex, string key);

    [LoggerMessage(Level = LogLevel.Information, Message = "[runner] Stopping agent: {Key}")]
    private partial void LogStoppingAgent(string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[runner] Session {Key} has no PID — forcibly removing orphaned session")]
    private partial void LogOrphanedSessionRemoved(string key);

    [LoggerMessage(Level = LogLevel.Error, Message = "[runner] Agent {Key} has missing or malformed agent.json; aborting launch")]
    private partial void LogAgentJsonMissingOrMalformed(string key);

    [LoggerMessage(Level = LogLevel.Error, Message = "[runner] Agent {Key} requested executor '{Executor}' which is not registered. Available: {Available}")]
    private partial void LogExecutorNotRegistered(string key, string executor, string available);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[runner] Agent {Key}: model '{Model}' is not registered for executor '{Executor}'. This may cause a runtime failure if the model does not exist.")]
    private partial void LogModelNotRegistered(string key, string model, string executor);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[runner] Failed to read inbox directory {InboxDir}")]
    private partial void LogReadInboxDirectoryFailed(Exception ex, string inboxDir);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[runner] Failed to write PID for {Key}")]
    private partial void LogWritePidFailed(Exception? ex, string key);

    [LoggerMessage(Level = LogLevel.Information, Message = "[runner] Launched {Key} PID={Pid}")]
    private partial void LogLaunchedWithPid(string key, int pid);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[runner] {Key}: {Warning}")]
    private partial void LogAgentWarning(string key, string warning);

    [LoggerMessage(Level = LogLevel.Error, Message = "[runner] Agent {Key} failed with exit code {Code}: {Error}")]
    private partial void LogAgentFailedWithExitCode(string key, int code, string? error);

    [LoggerMessage(Level = LogLevel.Information, Message = "[runner] Agent {Key} cancelled")]
    private partial void LogAgentCancelled(string key);

    [LoggerMessage(Level = LogLevel.Error, Message = "[runner] Agent {Key} error")]
    private partial void LogAgentError(Exception ex, string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[runner] Transcript flush faulted for {Key}")]
    private partial void LogTranscriptFlushFaulted(Exception ex, string key);

    [LoggerMessage(Level = LogLevel.Information, Message = "[runner] Agent {Key} finished (exit={Code}) at {Time}")]
    private partial void LogAgentFinished(string key, int code, DateTime time);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[runner] Agent {Key} hit a rate limit — inbox NOT archived, launches suppressed until {Until}")]
    private partial void LogAgentRateLimited(string key, DateTime until);
}
