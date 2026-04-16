using AiDev.Executors;
using AiDev.Features.Insights;
using AiDev.Features.KnowledgeBase;
using AiDev.Features.Playbook;
using AiDev.Features.Secrets;

namespace AiDev.Features.Agent;

/// <summary>
/// Manages launching and stopping agent sessions.
/// Selects the appropriate IAgentExecutor, builds the ExecutorContext (prompt, skills, model),
/// streams output to a transcript file, and handles inbox archiving and rate-limit suppression.
/// </summary>
public class AgentRunnerService(
    WorkspacePaths paths,
    StudioSettingsService settings,
    IEnumerable<IAgentExecutor> executors,
    IModelRegistry modelRegistry,
    MessageChangedNotifier messageNotifier,
    KbService kbService,
    PlaybookService playbookService,
    SecretsService secretsService,
    InsightsService insightsService,
    ILogger<AgentRunnerService> logger)
{
    private static readonly ActivitySource ActivitySource = new("AiDevNet.AgentRunner");
    private readonly Dictionary<string, IAgentExecutor> _executors =
        executors.GroupBy(e => e.Name).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    private const string AgentPrompt =
        "Read your inbox and action any messages. Follow your CLAUDE.md session protocol.";
    private const string ProjectScopedMcpPrompt =
        "Your assigned project slug is '{0}' and your agent slug is '{1}'. " +
        "For every MCP workspace tool call, pass projectSlug='{0}'. " +
        "Wherever your CLAUDE.md instructions say '{your-slug}', substitute '{1}'.";

    private sealed class SessionInfo(CancellationTokenSource cts)
    {
        public required ProjectSlug ProjectSlug { get; init; }
        public required AgentSlug AgentSlug { get; init; }
        public required DateTime StartedAt { get; init; }
        public AgentLaunchTrigger? Trigger { get; init; }
        public int Pid { get; set; }
        public CancellationTokenSource Cts { get; } = cts;
    }

    // Holds config loaded from agent.json that is needed across the session lifecycle.
    private sealed record AgentConfig(string ModelAlias, AgentExecutorName Executor, IReadOnlyList<string> Skills, ThinkingLevel ThinkingLevel = ThinkingLevel.Off);

    private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();
    private readonly ConcurrentDictionary<string, DateTime> _rateLimitedUntil = new();
    // Per-key semaphores to serialize usage file reads/writes when concurrent sessions finish same-day.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _usageLocks = new();

    private static string Key(ProjectSlug project, AgentSlug agent) => $"{project.Value}/{agent.Value}";

    // -------------------------------------------------------------------------
    // Public surface
    // -------------------------------------------------------------------------

    public bool IsRunning(ProjectSlug projectSlug, AgentSlug agentSlug) =>
        _sessions.ContainsKey(Key(projectSlug, agentSlug));

    public bool IsRateLimited(ProjectSlug projectSlug, AgentSlug agentSlug) =>
        _rateLimitedUntil.TryGetValue(Key(projectSlug, agentSlug), out var until) && DateTime.UtcNow < until;

    public IReadOnlyList<RunningSession> GetRunningSessions() =>
        _sessions.Values.Select(s => new RunningSession
        {
            ProjectSlug = s.ProjectSlug,
            AgentSlug = s.AgentSlug,
            Pid = s.Pid,
            StartedAt = s.StartedAt,
        }).ToList();

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
                var jsonPath = Path.Combine(agentDir, "agent.json");
                if (!File.Exists(jsonPath)) continue;

                try
                {
                    var json = await File.ReadAllTextAsync(jsonPath);
                    var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (!doc.RootElement.TryGetProperty("status", out var statusEl)) continue;
                    if (!string.Equals(statusEl.GetString(), "running", StringComparison.OrdinalIgnoreCase)) continue;

                    // Check whether this process actually owns a live session for this agent.
                    var slug = AgentSlug.TryParse(Path.GetFileName(agentDir), out var s) ? s : null;
                    if (slug is null) continue;
                    if (_sessions.ContainsKey(Key(project, slug))) continue;

                    logger.LogWarning(
                        "[runner] Recovering stale running state for {Project}/{Agent} — resetting to idle",
                        project.Value, slug.Value);

                    await UpdateAgentStatusAsync(agentDir, new()
                    {
                        ["status"] = "idle",
                        ["pid"] = null,
                        ["sessionStartedAt"] = null,
                    });
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[runner] Failed to inspect {Path} during stale-session recovery", jsonPath);
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
            logger.LogInformation("[runner] Agent already running: {Key}", key);
            return false;
        }

        if (_rateLimitedUntil.TryGetValue(key, out var until) && DateTime.UtcNow < until)
        {
            logger.LogInformation("[runner] Agent rate-limited until {Until}, skipping launch: {Key}", until, key);
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

        using var activity = ActivitySource.StartActivity("Agent.Launch", ActivityKind.Server);
        activity?.SetTag("agent.project", projectSlug);
        activity?.SetTag("agent.slug", agentSlug);
        activity?.SetTag("agent.startedAt", startedAt.ToString("o"));
        ApplyTriggerTags(activity, trigger);
        _ = RunSessionAsync(key, info, startedAt, activity?.Id);
        return true;
    }

    /// <summary>
    /// Signals a running agent to stop.
    /// </summary>
    public bool StopAgent(ProjectSlug projectSlug, AgentSlug agentSlug)
    {
        var key = Key(projectSlug, agentSlug);
        if (!_sessions.TryGetValue(key, out var info)) return false;
        logger.LogInformation("[runner] Stopping agent: {Key}", key);
        info.Cts.Cancel();
        return true;
    }

    /// <summary>
    /// Returns the token usage from the most recent session for this agent, or null if none exists.
    /// </summary>
    public TokenUsage? GetLastSessionUsage(ProjectSlug projectSlug, AgentSlug agentSlug)
    {
        var transcriptDir = paths.AgentTranscriptsDir(projectSlug, agentSlug);
        if (!Directory.Exists(transcriptDir)) return null;

        var usageFile = Directory.GetFiles(transcriptDir, "*.usage.json")
            .OrderByDescending(f => f)
            .FirstOrDefault();

        if (usageFile == null) return null;

        try
        {
            var json = File.ReadAllText(usageFile);
            return System.Text.Json.JsonSerializer.Deserialize<TokenUsage>(json, JsonDefaults.Read);
        }
        catch { return null; }
    }

    public TokenUsage? GetSessionUsage(ProjectSlug projectSlug, AgentSlug agentSlug, TranscriptDate date)
    {
        var transcriptDir = paths.AgentTranscriptsDir(projectSlug, agentSlug);
        var usagePath = Path.Combine(transcriptDir, $"{date.Value}.usage.json");
        if (!File.Exists(usagePath)) return null;
        try
        {
            var json = File.ReadAllText(usagePath);
            return System.Text.Json.JsonSerializer.Deserialize<TokenUsage>(json, JsonDefaults.Read);
        }
        catch { return null; }
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

        var inboxSnapshot = Array.Empty<string>();
        if (Directory.Exists(inboxDir))
        {
            try { inboxSnapshot = Directory.GetFiles(inboxDir, "*.md").Select(Path.GetFileName).OfType<string>().OrderBy(f => f).ToArray(); }
            catch { /* ignore */ }
        }

        // Load agent config — fail fast on missing or malformed agent.json rather than
        // silently defaulting to a different executor/model.
        var agentConfig = LoadAgentConfig(agentDir);
        if (agentConfig == null)
        {
            logger.LogError("[runner] Agent {Key} has missing or malformed agent.json; aborting launch", key);
            await UpdateAgentStatusAsync(agentDir, new()
            {
                ["lastError"] = "Missing or malformed agent.json; cannot determine executor and model.",
                ["lastErrorAt"] = startedAt.ToString("o"),
                ["status"] = "error",
            });
            _sessions.TryRemove(key, out _);
            return;
        }

        var modelId = ResolveModelId(agentConfig.ModelAlias, agentConfig.Executor);
        var executorName = agentConfig.Executor;
        activity?.SetTag("agent.executor", executorName.Value);

        if (!_executors.TryGetValue(executorName.Value, out var resolvedExecutor))
        {
            var available = string.Join(", ", _executors.Keys);
            logger.LogError("[runner] Agent {Key} requested executor '{Executor}' which is not registered. Available: {Available}",
                key, executorName.Value, available);
            await UpdateAgentStatusAsync(agentDir, new()
            {
                ["lastError"] = $"Executor '{executorName.Value}' is not registered. Available: {available}",
                ["lastErrorAt"] = startedAt.ToString("o"),
                ["status"] = "error",
            });
            _sessions.TryRemove(key, out _);
            return;
        }

        // Warn if the model is not known to the registry for this executor.
        // For dynamic executors (Ollama, GitHub Models) the registry may not have data until
        // health checks run, so this is advisory only — we do not block launch.
        if (modelRegistry.Find(executorName.Value, modelId) == null)
        {
            logger.LogWarning("[runner] Agent {Key}: model '{Model}' is not registered for executor '{Executor}'. " +
                "This may cause a runtime failure if the model does not exist.",
                key, modelId, executorName.Value);
        }

        await UpdateAgentStatusAsync(agentDir, new()
        {
            ["status"] = "running",
            ["lastRunAt"] = startedAt.ToString("o"),
            ["sessionStartedAt"] = startedAt.ToString("o"),
        });

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

        // Build prompt: inject playbook and KB articles before the standard instruction.
        var effectivePrompt = string.Join("\n\n",
            string.Format(ProjectScopedMcpPrompt, projectSlug.Value, agentSlug.Value),
            AgentPrompt);
        var inboxText = ReadInboxText(inboxDir, inboxSnapshot);

        var kbContext = kbService.BuildInjectionContext(projectSlug, inboxText);
        if (!string.IsNullOrEmpty(kbContext))
        {
            effectivePrompt = kbContext + "\n\n---\n\n" + effectivePrompt;
            logger.LogInformation("[runner] Injected KB context into prompt for {Key}", key);
        }

        var playbookSlug = ExtractPlaybookSlug(inboxDir, inboxSnapshot);
        if (playbookSlug != null)
        {
            var playbookContext = playbookService.GetInjectionContext(projectSlug, playbookSlug);
            if (!string.IsNullOrEmpty(playbookContext))
            {
                effectivePrompt = playbookContext + "\n\n---\n\n" + effectivePrompt;
                logger.LogInformation("[runner] Injected playbook '{Slug}' into prompt for {Key}", playbookSlug, key);
            }
            else
            {
                logger.LogWarning("[runner] Playbook '{Slug}' specified in inbox message not found for {Key}", playbookSlug, key);
            }
        }

        var exitCode = 0;
        var isRateLimited = false;
        var preserveInbox = false;
        string? sessionError = null;
        TokenUsage? sessionUsage = null;

        // Load project secrets for environment injection — values are sensitive, never log them.
        var secrets = secretsService.LoadDecryptedSecrets(projectSlug);

        var context = new ExecutorContext(
            WorkspaceRoot: paths.Root,
            ProjectSlug: projectSlug.Value,
            WorkingDir: agentDir,
            ModelId: modelId,
            Prompt: effectivePrompt,
            CancellationToken: info.Cts.Token,
            EnabledSkills: agentConfig.Skills,
            ReportPid: pid =>
            {
                info.Pid = pid;
                _ = UpdateAgentStatusAsync(agentDir, new() { ["pid"] = pid });
                logger.LogInformation("[runner] Launched {Key} PID={Pid}", key, pid);
                activity?.SetTag("agent.pid", pid);
                activity?.AddEvent(new("process.started"));
            },
            Trigger: info.Trigger,
            ThinkingLevel: agentConfig.ThinkingLevel,
            Secrets: secrets.Count > 0 ? secrets : null);

        try
        {
            var result = await resolvedExecutor.RunAsync(context, outputChannel.Writer);
            exitCode = result.ExitCode;
            isRateLimited = result.IsRateLimited;
            preserveInbox = result.PreserveInbox;
            sessionError = result.ErrorMessage;
            sessionUsage = result.Usage;

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

                logger.LogError("[runner] Agent {Key} failed with exit code {Code}: {Error}", key, exitCode, sessionError);
                activity?.SetTag("agent.error", true);
                activity?.SetTag("agent.errorMessage", sessionError);
                activity?.SetStatus(ActivityStatusCode.Error, sessionError);
                activity?.AddEvent(new("process.error"));
            }
        }
        catch (OperationCanceledException)
        {
            exitCode = 130;
            logger.LogInformation("[runner] Agent {Key} cancelled", key);
            activity?.SetTag("agent.cancelled", true);
            activity?.AddEvent(new("process.cancelled"));
        }
        catch (Exception ex)
        {
            exitCode = 1;
            sessionError = ex.Message;
            logger.LogError(ex, "[runner] Agent {Key} error", key);
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
            catch (Exception ex) { logger.LogWarning(ex, "[runner] Transcript flush faulted for {Key}", key); }

            // Persist token usage alongside the transcript (accumulate across same-day sessions).
            if (sessionUsage != null)
            {
                try
                {
                    var usagePath = Path.Combine(transcriptDir, $"{transcriptDate.Value}.usage.json");
                    // Serialize concurrent session finishes on the same key to avoid TOCTOU on the usage file.
                    var usageLock = _usageLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
                    // Bounded wait — if the semaphore is somehow never released we should not hang forever.
                    if (await usageLock.WaitAsync(TimeSpan.FromSeconds(30)))
                    {
                        try
                        {
                            if (File.Exists(usagePath))
                            {
                                try
                                {
                                    var existing = System.Text.Json.JsonSerializer.Deserialize<TokenUsage>(
                                        await File.ReadAllTextAsync(usagePath), JsonDefaults.Read);
                                    if (existing != null) sessionUsage = existing + sessionUsage;
                                }
                                catch { /* ignore corrupt existing file; overwrite with current session */ }
                            }
                            var usageJson = System.Text.Json.JsonSerializer.Serialize(sessionUsage, JsonDefaults.Write);
                            await File.WriteAllTextAsync(usagePath, usageJson);
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

            activity?.SetTag("agent.finishedAt", exitedAt.ToString("o"));
            activity?.AddEvent(new("session.finished"));
            logger.LogInformation("[runner] Agent {Key} finished (exit={Code}) at {Time}", key, exitCode, exitedAt);

            // Write final status to agent.json — wrapped so a disk error can't abort
            // inbox archival or the relaunch check below.
            await UpdateAgentStatusAsync(agentDir, new()
            {
                ["status"] = exitCode is 0 or 130 ? "idle" : "error",
                ["pid"] = null,
                ["sessionStartedAt"] = null,
                ["lastError"] = exitCode == 0 || exitCode == 130 || string.IsNullOrWhiteSpace(sessionError) ? null : sessionError,
                ["lastErrorAt"] = exitCode == 0 || exitCode == 130 || string.IsNullOrWhiteSpace(sessionError) ? null : exitedAt.ToString("o"),
            });

            // Generate AI insights for the completed session (fire-and-forget; CancellationToken.None so
            // insights finish writing even after the session CT is cancelled at shutdown).
            var insightPath = paths.InsightPath(projectSlug, agentSlug, transcriptDate).Value;
            _ = insightsService.GenerateAndSaveAsync(transcriptPath, insightPath, CancellationToken.None)
                .ContinueWith(t =>
                {
                    if (t.Exception != null)
                        logger.LogWarning(t.Exception, "[runner] Insights generation faulted for {Project}/{Agent}",
                            projectSlug.Value, agentSlug.Value);
                }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

            if (isRateLimited)
            {
                var suppressUntil = DateTime.UtcNow.AddMinutes(30);
                _rateLimitedUntil[key] = suppressUntil;
                logger.LogWarning(
                    "[runner] Agent {Key} hit a rate limit — inbox NOT archived, launches suppressed until {Until}",
                    key, suppressUntil);
                messageNotifier.Notify(projectSlug);
            }
            else
            {
                _rateLimitedUntil.TryRemove(key, out _);
                if (preserveInbox)
                {
                    logger.LogWarning(
                        "[runner] Agent {Key} ended with a recoverable configuration error — inbox preserved for retry",
                        key);
                    if (inboxSnapshot.Length > 0) messageNotifier.Notify(projectSlug);
                }
                else
                {
                    ArchiveInbox(inboxDir, inboxSnapshot);
                    if (inboxSnapshot.Length > 0) messageNotifier.Notify(projectSlug);

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
                        activity?.SetTag("agent.relaunch", true);
                        activity?.SetTag("agent.relaunchReason", "inbox-messages-during-session");
                        LaunchAgent(projectSlug, agentSlug);
                    }
                }
            }
        }
    }

    private static void ApplyTriggerTags(Activity? activity, AgentLaunchTrigger? trigger)
    {
        if (activity == null || trigger == null)
            return;

        activity.SetTag("agent.trigger.source", trigger.Source);
        activity.SetTag("agent.trigger.reason", trigger.Reason);
        if (!string.IsNullOrWhiteSpace(trigger.ProjectSlug))
            activity.SetTag("project.slug", trigger.ProjectSlug);
        if (!string.IsNullOrWhiteSpace(trigger.TaskId))
            activity.SetTag("task.id", trigger.TaskId);
        if (!string.IsNullOrWhiteSpace(trigger.DecisionId))
            activity.SetTag("decision.id", trigger.DecisionId);
        if (!string.IsNullOrWhiteSpace(trigger.MessageFile))
            activity.SetTag("message.file", trigger.MessageFile);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static AgentConfig? LoadAgentConfig(string agentDir)
    {
        const string DefaultModel = "claude-sonnet-4-6";
        var path = Path.Combine(agentDir, "agent.json");
        if (!File.Exists(path)) return null;
        try
        {
            var raw = File.ReadAllText(path);
            var doc = System.Text.Json.JsonDocument.Parse(raw);
            var root = doc.RootElement;

            var executor = root.TryGetProperty("executor", out var e)
                && AgentExecutorName.TryParse(e.GetString(), out var configuredExecutor)
                    ? configuredExecutor
                    : AgentExecutorName.Default;

            var rawModel = root.TryGetProperty("model", out var m) ? m.GetString() ?? DefaultModel : DefaultModel;
            // Resolve old alias at runtime — guards against agents launched before migration ran.
            var model = LegacyModelAliases.Resolve(rawModel, executor.Value) ?? rawModel;

            List<string> skills = [];
            if (root.TryGetProperty("skills", out var s) && s.ValueKind == System.Text.Json.JsonValueKind.Array)
                skills = s.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToList();

            var thinking = root.TryGetProperty("thinking", out var th)
                ? ThinkingLevelExtensions.Parse(th.GetString())
                : ThinkingLevel.Off;

            return new(ModelAlias: model, Executor: executor, Skills: skills, ThinkingLevel: thinking);
        }
        catch { return null; }
    }

    private string ResolveModelId(string modelOrAlias, AgentExecutorName executor)
    {
        if (string.IsNullOrWhiteSpace(modelOrAlias))
            return modelOrAlias;

        var configuredModels = settings.GetSettings().Models;
        if (configuredModels.TryGetValue(modelOrAlias, out var configuredModelId)
            && !string.IsNullOrWhiteSpace(configuredModelId))
            return configuredModelId;

        return LegacyModelAliases.Resolve(modelOrAlias, executor.Value) ?? modelOrAlias;
    }

    private static async Task UpdateAgentStatusAsync(string agentDir, Dictionary<string, object?> updates)
    {
        var path = Path.Combine(agentDir, "agent.json");
        try
        {
            // If the file is corrupt, skip the update entirely — better to leave it unchanged
            // than to overwrite it with only status fields, which would destroy slug/model/executor config.
            Dictionary<string, JsonElement> existing = [];
            if (File.Exists(path))
            {
                existing = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    await File.ReadAllTextAsync(path), JsonDefaults.Read) ?? [];
            }

            var merged = existing.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
            foreach (var (k, v) in updates)
            {
                if (v == null) merged.Remove(k);
                // Serialize each value to JsonElement to guarantee proper JSON escaping
                // (avoids issues with special characters in strings going through object? boxing).
                else merged[k] = System.Text.Json.JsonSerializer.SerializeToElement(v);
            }

            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(merged, JsonDefaults.Write));
        }
        catch { /* don't crash session if status write fails */ }
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

    private static string? ExtractPlaybookSlug(string inboxDir, string[] snapshot)
    {
        foreach (var filename in snapshot)
        {
            try
            {
                var path = Path.Combine(inboxDir, filename);
                if (!File.Exists(path)) continue;
                var content = File.ReadAllText(path);
                var (fields, _) = FrontmatterParser.Parse(content);
                if (fields.TryGetValue("playbook", out var slug) && !string.IsNullOrWhiteSpace(slug))
                    return slug.Trim();
            }
            catch { /* ignore unreadable files */ }
        }
        return null;
    }

    private static string ReadInboxText(string inboxDir, string[] snapshot)
    {
        if (snapshot.Length == 0) return string.Empty;
        var sb = new System.Text.StringBuilder();
        foreach (var filename in snapshot)
        {
            try
            {
                var path = Path.Combine(inboxDir, filename);
                if (File.Exists(path)) sb.AppendLine(File.ReadAllText(path));
            }
            catch { /* ignore unreadable files */ }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Writes a human-readable message to an agent's inbox.
    /// </summary>
    public string? WriteInboxMessage(ProjectSlug projectSlug, AgentSlug agentSlug,
        string from, string re, string type, string priority, string body, TaskId? taskId = null, string? decisionId = null)
    {
        using var activity = ActivitySource.StartActivity("Agent.WriteInboxMessage", ActivityKind.Internal);
        activity?.SetTag("agent.project", projectSlug);
        activity?.SetTag("agent.slug", agentSlug);
        activity?.SetTag("message.from", from);
        activity?.SetTag("message.type", type);
        activity?.SetTag("message.priority", priority);

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
                ["priority"] = priority,
                ["re"] = re,
                ["type"] = type,
            };
            if (taskId != null) fields["task-id"] = taskId.ToString();
            if (decisionId != null) fields["decision-id"] = decisionId;
            var content = FrontmatterParser.Stringify(fields, body);
            File.WriteAllText(filePath, content);
            activity?.SetTag("message.filename", unique);
            activity?.SetTag("message.success", true);
            logger.LogInformation("[runner] Inbox message written: {Project}/{Agent} ← {From} ({Type}) [{File}]",
                projectSlug, agentSlug, from, type, unique);
            return null;
        }
        catch (Exception ex)
        {
            activity?.SetTag("message.success", false);
            activity?.SetTag("message.error", ex.Message);
            logger.LogError(ex, "[runner] Failed to write inbox message to {Project}/{Agent} from {From}: {Error}",
                projectSlug, agentSlug, from, ex.Message);
            return ex.Message;
        }
    }
}
