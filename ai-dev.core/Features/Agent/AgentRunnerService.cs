using AiDev.Executors;
using AiDev.Features.KnowledgeBase;
using AiDev.Features.Playbook;
using AiDev.Models;
using AiDev.Services;

namespace AiDev.Features.Agent;

/// <summary>
/// Manages launching and stopping Claude CLI processes for agents.
/// Each agent runs in its own directory with its CLAUDE.md as context.
/// </summary>
public class AgentRunnerService(
    WorkspacePaths paths,
    StudioSettingsService settings,
    IEnumerable<IAgentExecutor> executors,
    MessageChangedNotifier messageNotifier,
    KbService kbService,
    PlaybookService playbookService,
    ILogger<AgentRunnerService> logger)
{

    private static readonly ActivitySource ActivitySource = new("AiDevNet.AgentRunner");
    private readonly Dictionary<string, IAgentExecutor> _executors =
        executors.GroupBy(e => e.Name).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    private const string AgentPrompt =
        "Read your inbox and action any messages. Follow your CLAUDE.md session protocol.";

    private sealed class SessionInfo(CancellationTokenSource cts)
    {
        public required ProjectSlug ProjectSlug { get; init; }
        public required AgentSlug AgentSlug { get; init; }
        public required DateTime StartedAt { get; init; }
        public int Pid { get; set; }
        public CancellationTokenSource Cts { get; } = cts;
    }

    private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();

    // Per-agent rate-limit suppression: maps session key → UTC time after which launches are allowed again.
    private readonly ConcurrentDictionary<string, DateTime> _rateLimitedUntil = new();

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
    /// Launches an agent. Returns false if already running or rate-limited.
    /// The process runs in the background — this method returns quickly.
    /// </summary>
    public bool LaunchAgent(ProjectSlug projectSlug, AgentSlug agentSlug)
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
        };

        if (!_sessions.TryAdd(key, info))
            return false; // race condition — another caller won

        // Start an OTEL activity for agent launch
        using var activity = ActivitySource.StartActivity("Agent.Launch", ActivityKind.Server);
        activity?.SetTag("agent.project", projectSlug);
        activity?.SetTag("agent.slug", agentSlug);
        activity?.SetTag("agent.startedAt", startedAt.ToString("o"));
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

    private async Task RunSessionAsync(string key, SessionInfo info, DateTime startedAt, string? parentActivityId = null)
    {
        var projectSlug = info.ProjectSlug;
        var agentSlug = info.AgentSlug;

        using var activity = ActivitySource.StartActivity("Agent.RunSession", ActivityKind.Internal, parentActivityId);
        activity?.SetTag("agent.project", projectSlug);
        activity?.SetTag("agent.slug", agentSlug);
        activity?.SetTag("agent.sessionStartedAt", startedAt.ToString("o"));
        var agentDir = paths.AgentDir(projectSlug, agentSlug);
        var inboxDir = paths.AgentInboxDir(projectSlug, agentSlug);

        // Snapshot inbox files present at launch so we can archive them on exit
        var inboxSnapshot = Array.Empty<string>();
        if (Directory.Exists(inboxDir))
        {
            try { inboxSnapshot = Directory.GetFiles(inboxDir, "*.md").Select(Path.GetFileName).OfType<string>().OrderBy(f => f).ToArray(); }
            catch { /* ignore */ }
        }

        // Resolve model alias → full model ID
        var agentInfo = LoadAgentJson(agentDir);
        var modelAlias = agentInfo?.GetValueOrDefault("model")?.ToString() ?? "sonnet";
        var modelId = settings.GetSettings().Models.GetValueOrDefault(modelAlias, modelAlias);

        // Resolve executor
        var executorName = agentInfo?.GetValueOrDefault("executor")?.ToString();
        if (string.IsNullOrWhiteSpace(executorName)) executorName = IAgentExecutor.Default;
        activity?.SetTag("agent.executor", executorName);

        if (!_executors.TryGetValue(executorName, out var resolvedExecutor))
        {
            var available = string.Join(", ", _executors.Keys);
            logger.LogError("[runner] Agent {Key} requested executor '{Executor}' which is not registered. Available: {Available}",
                key, executorName, available);
            _sessions.TryRemove(key, out _);
            return;
        }

        // Update agent.json: status = running
        await UpdateAgentStatusAsync(agentDir, new()
        {
            ["status"] = "running",
            ["lastRunAt"] = startedAt.ToString("o"),
            ["sessionStartedAt"] = startedAt.ToString("o"),
        });

        var exitCode = 0;

        // Channel decouples executor output from the async transcript writer.
        // The consumer task owns the StreamWriter entirely — no transcript reference leaks into
        // the outer scope or across a Task.Run boundary.
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
            await transcript.WriteLineAsync($"executor: {executorName} · model: {modelId}");
            await transcript.WriteLineAsync();
            await transcript.FlushAsync();
            await foreach (var line in outputChannel.Reader.ReadAllAsync())
            {
                await transcript.WriteLineAsync(line);
                await transcript.FlushAsync();
            }
        });

        // Build prompt: optionally inject a playbook (specified in message frontmatter),
        // then any matching KB articles, before the standard instruction.
        var effectivePrompt = AgentPrompt;
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

        try
        {
            exitCode = await resolvedExecutor.RunAsync(
                agentDir, modelId, effectivePrompt, outputChannel.Writer,
                pid =>
                {
                    info.Pid = pid;
                    _ = UpdateAgentStatusAsync(agentDir, new() { ["pid"] = pid });
                    logger.LogInformation("[runner] Launched {Key} PID={Pid}", key, pid);
                    activity?.SetTag("agent.pid", pid);
                    activity?.AddEvent(new("process.started"));
                },
                info.Cts.Token);

            activity?.SetTag("agent.exitCode", exitCode);
            activity?.AddEvent(new("process.exited"));
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
            logger.LogError(ex, "[runner] Agent {Key} error", key);
            activity?.SetTag("agent.error", true);
            activity?.SetTag("agent.errorMessage", ex.Message);
            activity?.AddEvent(new("process.error"));
            outputChannel.Writer.TryWrite($"[{DateTime.UtcNow:o}] [error] {ex.Message}");
        }
        finally
        {
            var exitedAt = DateTime.UtcNow;

            // Route footer through the channel so the consumer writes it in sequence,
            // then complete and drain before any other cleanup touches the file.
            outputChannel.Writer.TryWrite(string.Empty);
            outputChannel.Writer.TryWrite($"## Session ended at {exitedAt:o} (exit code: {exitCode})");
            outputChannel.Writer.TryComplete();
            await consumerTask; // StreamWriter is disposed inside here

            activity?.SetTag("agent.finishedAt", exitedAt.ToString("o"));
            activity?.AddEvent(new("session.finished"));
            logger.LogInformation("[runner] Agent {Key} finished (exit={Code}) at {Time}", key, exitCode, exitedAt);

            _sessions.TryRemove(key, out _);

            await UpdateAgentStatusAsync(agentDir, new()
            {
                ["status"] = "idle",
                ["pid"] = null,
                ["sessionStartedAt"] = null,
            });

            // Detect a rate-limited session: transcript only contains the rate-limit message,
            // session exited almost immediately. Don't archive the inbox (messages were never read)
            // and suppress re-launches until the limit resets.
            if (exitCode == 0 && SessionWasRateLimited(transcriptPath))
            {
                var suppressUntil = DateTime.UtcNow.AddMinutes(30);
                _rateLimitedUntil[key] = suppressUntil;
                logger.LogWarning(
                    "[runner] Agent {Key} hit a rate limit — inbox NOT archived, launches suppressed until {Until}",
                    key, suppressUntil);
                activity?.SetTag("agent.rateLimited", true);
                messageNotifier.Notify(projectSlug); // let the UI refresh status
            }
            else
            {
                _rateLimitedUntil.TryRemove(key, out _); // clear any prior rate-limit state on successful run
                ArchiveInbox(inboxDir, inboxSnapshot);
                if (inboxSnapshot.Length > 0) messageNotifier.Notify(projectSlug);

                // Messages can arrive in the inbox while the session is running and the
                // FileSystemWatcher skips them (agent already running). Check after archiving
                // so we only count messages that arrived AFTER the session snapshot was taken.
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

    private static Dictionary<string, object?>? LoadAgentJson(string agentDir)
    {
        var path = Path.Combine(agentDir, "agent.json");
        if (!File.Exists(path)) return null;
        try
        {
            var raw = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(raw,
                JsonDefaults.Read)
                ?.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
        }
        catch { return null; }
    }

    private static async Task UpdateAgentStatusAsync(string agentDir, Dictionary<string, object?> updates)
    {
        var path = Path.Combine(agentDir, "agent.json");
        try
        {
            var existing = File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    await File.ReadAllTextAsync(path),
                    JsonDefaults.Read)
                  ?? []
                : [];

            var merged = existing.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
            foreach (var (k, v) in updates)
            {
                if (v == null) merged.Remove(k);
                else merged[k] = v;
            }

            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(merged,
                JsonDefaults.Write));
        }
        catch { /* don't crash session if status write fails */ }
    }

    /// <summary>
    /// Reads the session transcript and returns true if the session ended immediately
    /// due to a rate limit (no real work was performed and the inbox should NOT be archived).
    /// </summary>
    private static bool SessionWasRateLimited(string transcriptPath)
    {
        try
        {
            var text = File.ReadAllText(transcriptPath);
            // Claude CLI prints one of these and exits immediately when rate-limited.
            return text.Contains("hit your limit", StringComparison.OrdinalIgnoreCase)
                || text.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase)
                || text.Contains("RateLimitError", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
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

    /// <summary>
    /// Scans inbox messages for a <c>playbook:</c> frontmatter field and returns the
    /// first non-empty slug found, or <c>null</c> if no message specifies a playbook.
    /// </summary>
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

    /// <summary>
    /// Reads the content of pending inbox files so trigger matching can compare
    /// KB article triggers against the actual task text.
    /// </summary>
    private static string ReadInboxText(string inboxDir, string[] snapshot)
    {
        if (snapshot.Length == 0) return string.Empty;
        var sb = new System.Text.StringBuilder();
        foreach (var filename in snapshot)
        {
            try
            {
                var path = Path.Combine(inboxDir, filename);
                if (File.Exists(path))
                    sb.AppendLine(File.ReadAllText(path));
            }
            catch { /* ignore unreadable files */ }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Writes a human-readable message to an agent's inbox.
    /// </summary>
    public string? WriteInboxMessage(ProjectSlug projectSlug, AgentSlug agentSlug,
        string from, string re, string type, string priority, string body, TaskId? taskId = null)
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
            var filename = $"{now:yyyyMMdd-HHmmss}-from-{from}.md";
            var filePath = Path.Combine(inboxDir, filename);
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
            var content = FrontmatterParser.Stringify(fields, body);
            File.WriteAllText(filePath, content);
            activity?.SetTag("message.filename", filename);
            activity?.SetTag("message.success", true);
            logger.LogInformation("[runner] Inbox message written: {Project}/{Agent} ← {From} ({Type}) [{File}]",
                projectSlug, agentSlug, from, type, filename);
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
