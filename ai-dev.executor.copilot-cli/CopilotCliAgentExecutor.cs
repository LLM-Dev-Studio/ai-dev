using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

using AiDev.Models.Types;
using Microsoft.Extensions.Logging;

namespace AiDev.Executors;

/// <summary>
/// Runs agents via the GitHub Copilot CLI
/// (<c>copilot -p &lt;prompt&gt; --output-format json --allow-all-tools ...</c>).
///
/// The prompt is passed as a command-line argument because <c>-p</c> requires
/// inline text (unlike Claude's <c>-p -</c> stdin pattern). <see cref="ProcessStartInfo.ArgumentList"/>
/// handles escaping, and Windows' ~32 KB command-line limit is not a concern for
/// typical agent prompts.
///
/// Permission model:
///   Non-interactive mode requires <c>--allow-all-tools</c>. Skills are enforced
///   by adding <c>--deny-tool</c> entries for capabilities a disabled skill would
///   otherwise grant. The workspace MCP server is registered per-run via
///   <c>--additional-mcp-config @&lt;path&gt;</c> when the <c>mcp-workspace</c> skill
///   is enabled, writing a <c>.copilot/mcp-config.json</c> file at the project root.
///
/// Output parsing:
///   <c>--output-format json</c> emits JSONL, one event per line. Interesting types:
///     - <c>assistant.message_delta</c> — streaming chunks in <c>data.deltaContent</c>
///     - <c>assistant.message</c> — final per-turn content, tool requests, <c>outputTokens</c>
///     - <c>result</c> — session exit code and usage summary
///     - <c>error</c> / any <c>*.error</c> — surfaced to <c>ErrorMessage</c>
///   Session-lifecycle events (<c>session.*</c>, <c>assistant.turn_*</c>, <c>user.message</c>)
///   are suppressed.
///
/// Token usage:
///   Copilot reports <c>outputTokens</c> on each <c>assistant.message</c> event but does
///   not report input tokens — billing is subscription-based with a <c>premiumRequests</c>
///   counter in the final <c>result.usage</c>. We sum output tokens across messages and
///   leave input at zero.
/// </summary>
public class CopilotCliAgentExecutor(ILogger<CopilotCliAgentExecutor> logger) : IAgentExecutor
{
    public string Name => AgentExecutorName.CopilotCliValue;
    public string DisplayName => "Copilot CLI";
    public IReadOnlyList<ExecutorSkill> AvailableSkills => CopilotSkills.All;

    public IReadOnlyList<ModelDescriptor> KnownModels { get; } =
    [
        new("gpt-5.2", "GPT-5.2 (Copilot)", AgentExecutorName.CopilotCliValue,
            ModelCapabilities.Streaming | ModelCapabilities.ToolCalling | ModelCapabilities.Reasoning),
    ];

    // -------------------------------------------------------------------------
    // Health check
    // -------------------------------------------------------------------------

    public async Task<ExecutorHealthResult> CheckHealthAsync(CancellationToken ct = default)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "copilot",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("--version");

            using var proc = Process.Start(psi);
            if (proc == null)
                return new ExecutorHealthResult(false, "Failed to start copilot process");

            var stdout = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);

            // `copilot --version` prints something like:
            //   GitHub Copilot CLI 1.0.24.
            //   Run 'copilot update' to check for updates.
            // Keep just the first non-empty line.
            var firstLine = stdout
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault() ?? string.Empty;

            return proc.ExitCode == 0
                ? new ExecutorHealthResult(true, firstLine.Length > 0 ? firstLine : "copilot CLI found")
                : new ExecutorHealthResult(false, $"copilot --version exited with code {proc.ExitCode}");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new ExecutorHealthResult(false, $"Copilot CLI not found: {ex.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // Run
    // -------------------------------------------------------------------------

    public async Task<ExecutorResult> RunAsync(ExecutorContext context, ChannelWriter<string> output)
    {
        var skills = CopilotSkills.Resolve(context.EnabledSkills);

        var projectRoot = Path.Combine(context.WorkspaceRoot, context.ProjectSlug);
        var mcpConfigPath = skills.Contains(CopilotSkills.McpWorkspace.Key)
            ? WriteCopilotMcpConfig(projectRoot, context.WorkspaceRoot)
            : null;

        var psi = BuildProcessStartInfo(context, projectRoot, skills, mcpConfigPath);

        if (context.Secrets is { Count: > 0 } secrets)
        {
            foreach (var (name, value) in secrets)
                psi.Environment[name] = value;
        }

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var isRateLimited = false;
        string? errorMessage = null;
        TokenUsage? capturedUsage = null;

        long lastActivityTicks = DateTime.UtcNow.Ticks;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            Interlocked.Exchange(ref lastActivityTicks, DateTime.UtcNow.Ticks);

            if (!isRateLimited && IsRateLimitLine(e.Data))
            {
                isRateLimited = true;
                logger.LogWarning("[copilot] Rate limit detected in output");
            }

            var (visibleText, usage, failure) = ParseJsonLine(e.Data);

            if (failure is not null)
            {
                logger.LogError("[copilot] {FailureMessage}", failure);
                Interlocked.CompareExchange(ref errorMessage, failure, null);
            }
            else
            {
                logger.LogDebug("[copilot] {Line}", e.Data);
            }

            if (usage != null)
                Interlocked.Exchange(ref capturedUsage, capturedUsage == null ? usage : capturedUsage + usage);

            if (!string.IsNullOrEmpty(visibleText))
                output.TryWrite($"[{DateTime.UtcNow:o}] {visibleText}");
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            Interlocked.Exchange(ref lastActivityTicks, DateTime.UtcNow.Ticks);

            if (!isRateLimited && IsRateLimitLine(e.Data))
                isRateLimited = true;

            if (IsBenignStderrLine(e.Data))
            {
                logger.LogDebug("[copilot] [stderr/warn] {Line}", e.Data);
                return;
            }

            var failureMessage = $"stderr: {e.Data}";
            logger.LogError("[copilot] {FailureMessage}", failureMessage);
            Interlocked.CompareExchange(ref errorMessage, failureMessage, null);
            output.TryWrite($"[{DateTime.UtcNow:o}] [stderr] {e.Data}");
        };

        process.Start();
        context.ReportPid?.Invoke(process.Id);

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var stallCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        var stallTask = Task.Run(async () =>
        {
            const int StallCheckInterval = 120;
            while (!stallCts.Token.IsCancellationRequested)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(StallCheckInterval), stallCts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }

                var silentFor = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - Interlocked.Read(ref lastActivityTicks));
                if (silentFor.TotalSeconds >= StallCheckInterval)
                {
                    var msg = $"[stall-check] no output for {silentFor.TotalMinutes:F0}m — process PID={process.Id} still running";
                    logger.LogWarning("[copilot] {Message}", msg);
                    output.TryWrite($"[{DateTime.UtcNow:o}] {msg}");
                    context.ReportWarning?.Invoke(msg);
                }
            }
        }, stallCts.Token);

        try
        {
            await process.WaitForExitAsync(context.CancellationToken).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                errorMessage ??= $"Copilot exited with code {process.ExitCode}.";
            }

            return new ExecutorResult(process.ExitCode, IsRateLimited: isRateLimited, ErrorMessage: errorMessage, Usage: capturedUsage);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            throw;
        }
        finally
        {
            stallCts.Cancel();
            try { await stallTask.ConfigureAwait(false); } catch { /* best-effort */ }
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static ProcessStartInfo BuildProcessStartInfo(
        ExecutorContext context,
        string projectRoot,
        HashSet<string> skills,
        string? mcpConfigPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "copilot",
            WorkingDirectory = context.WorkingDir,
            UseShellExecute = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(context.Prompt);

        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("json");

        psi.ArgumentList.Add("--allow-all-tools");
        psi.ArgumentList.Add("--no-auto-update");
        psi.ArgumentList.Add("--no-ask-user");

        if (!string.IsNullOrWhiteSpace(context.ModelId))
        {
            psi.ArgumentList.Add("--model");
            psi.ArgumentList.Add(context.ModelId);
        }

        var effort = context.ThinkingLevel.ToReasoningEffort();
        if (effort is not null)
        {
            psi.ArgumentList.Add("--effort");
            psi.ArgumentList.Add(effort);
        }

        var codebasePath = ReadCodebasePath(projectRoot);
        if (!string.IsNullOrEmpty(codebasePath))
        {
            psi.ArgumentList.Add("--add-dir");
            psi.ArgumentList.Add(codebasePath);
        }

        if (mcpConfigPath is not null)
        {
            psi.ArgumentList.Add("--additional-mcp-config");
            psi.ArgumentList.Add("@" + mcpConfigPath);
        }

        foreach (var denied in CopilotSkills.ToDeniedTools(skills))
        {
            psi.ArgumentList.Add("--deny-tool");
            psi.ArgumentList.Add(denied);
        }

        return psi;
    }

    private static string? ReadCodebasePath(string projectRoot)
    {
        var projectJson = Path.Combine(projectRoot, "project.json");
        if (!File.Exists(projectJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(projectJson));
            return doc.RootElement.TryGetProperty("codebasePath", out var val) ? val.GetString() : null;
        }
        catch { return null; }
    }

    /// <summary>
    /// Writes <c>&lt;projectRoot&gt;/.copilot/mcp-config.json</c> registering the workspace
    /// MCP server, and returns the absolute path. Uses the same atomic temp-file-then-move
    /// write pattern as the Claude executor to survive concurrent agent launches.
    /// </summary>
    private static string WriteCopilotMcpConfig(string projectRoot, string workspaceRoot)
    {
        var repoRoot   = Path.GetFullPath(Path.Combine(workspaceRoot, ".."));
        var mcpProject = Path.Combine(repoRoot, "ai-dev.mcp");

        var copilotDir = Path.Combine(projectRoot, ".copilot");
        Directory.CreateDirectory(copilotDir);

        var config = new JsonObject
        {
            ["mcpServers"] = new JsonObject
            {
                [CopilotSkills.McpServerName] = new JsonObject
                {
                    ["type"]    = "stdio",
                    ["command"] = "dotnet",
                    ["args"]    = new JsonArray("run", "--no-build", "--project", mcpProject, "--", workspaceRoot),
                },
            },
        };

        var configPath = Path.Combine(copilotDir, "mcp-config.json");
        var json = config.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        var tmpPath = configPath + $".{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, configPath, overwrite: true);

        return configPath;
    }

    private static bool IsRateLimitLine(string line) =>
        line.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
        || line.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase)
        || line.Contains("premium request", StringComparison.OrdinalIgnoreCase)
        || line.Contains("subscription limit", StringComparison.OrdinalIgnoreCase)
        || line.Contains("quota exceeded", StringComparison.OrdinalIgnoreCase);

    private static bool IsBenignStderrLine(string line) =>
        line.Contains("ignoring extra certs", StringComparison.OrdinalIgnoreCase)
        || line.Contains("warn:", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Parses a JSONL event line from <c>copilot --output-format json</c>.
    /// Returns the visible transcript text (if any), token usage (on assistant.message events),
    /// and a failure message (on error events). All three may be null for non-text events.
    /// </summary>
    private static (string? VisibleText, TokenUsage? Usage, string? Failure)
        ParseJsonLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return (null, null, null);

        if (!line.TrimStart().StartsWith('{')) return (line, null, null);

        JsonElement root;
        try   { root = JsonDocument.Parse(line).RootElement; }
        catch { return (line, null, null); }

        var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
        var data = root.TryGetProperty("data", out var d) ? (JsonElement?)d : null;

        switch (type)
        {
            case "assistant.message_delta":
            {
                var delta = data?.TryGetProperty("deltaContent", out var dc) == true ? dc.GetString() : null;
                return (string.IsNullOrEmpty(delta) ? null : delta, null, null);
            }

            case "assistant.message":
            {
                // The full assembled content is in data.content; we've already streamed it via
                // message_delta events, so suppress it here to avoid duplication. We still use
                // this event for tool-call descriptions and output token counts.
                var sb = new System.Text.StringBuilder();

                if (data?.TryGetProperty("toolRequests", out var tr) == true && tr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var call in tr.EnumerateArray())
                    {
                        var toolName = call.TryGetProperty("name", out var tn) ? tn.GetString() ?? "tool" : "tool";
                        if (sb.Length > 0) sb.AppendLine();
                        sb.Append($"▶ {toolName}");

                        // Include stringy input params so the transcript shows what the tool was called with.
                        if (call.TryGetProperty("arguments", out var args) && args.ValueKind == JsonValueKind.Object)
                        {
                            var parts = new System.Text.StringBuilder();
                            foreach (var prop in args.EnumerateObject())
                            {
                                if (prop.Value.ValueKind == JsonValueKind.String)
                                {
                                    if (parts.Length > 0) parts.Append(", ");
                                    parts.Append($"{prop.Name}={prop.Value.GetString()}");
                                }
                            }
                            if (parts.Length > 0) sb.Append($"({parts})");
                        }
                    }
                }

                TokenUsage? usage = null;
                if (data?.TryGetProperty("outputTokens", out var ot) == true && ot.ValueKind == JsonValueKind.Number)
                {
                    var outputTokens = ot.GetInt64();
                    if (outputTokens > 0)
                        usage = new TokenUsage(InputTokens: 0, OutputTokens: outputTokens);
                }

                return (sb.Length > 0 ? sb.ToString() : null, usage, null);
            }

            case "assistant.error":
            case "error":
            {
                string? msg = null;
                if (data?.TryGetProperty("message", out var dm) == true)
                    msg = dm.GetString();
                else if (root.TryGetProperty("message", out var rm))
                    msg = rm.GetString();
                else if (root.TryGetProperty("error", out var err))
                    msg = err.ValueKind == JsonValueKind.String ? err.GetString() : err.ToString();

                return (null, null, msg ?? line);
            }

            case "result":
            {
                // Non-zero exit is surfaced as an error via the process exit handler; nothing to emit here.
                // Copilot's usage section carries premiumRequests / durations rather than token counts,
                // so per-message outputTokens (already captured above) remains our only usage signal.
                return (null, null, null);
            }
        }

        // session.*, user.*, assistant.turn_* and unknown events — suppress.
        return (null, null, null);
    }
}
