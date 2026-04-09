using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace AiDev.Executors;

/// <summary>
/// Runs agents via the Claude CLI (`claude --print - --model &lt;id&gt; --allowedTools ...`).
/// The prompt is written to stdin to avoid Windows command-line length and newline-escaping issues.
/// On Windows, wraps the call in `cmd.exe /c` because the `claude` npm global is a .cmd file.
///
/// Permission model:
///   Agents connect to the ai-dev.mcp MCP server registered in .claude/settings.json.
///   The MCP server enforces path boundaries and logs all operations. Raw Read/Write/Edit/Bash
///   tools are denied by settings.json — agents can only use workspace operations exposed by
///   the MCP server. Git tools are granted via --allowedTools based on the agent's skill list.
///
/// Rate-limit detection:
///   Output lines are scanned as they stream. When a rate-limit signal is detected, the result
///   carries IsRateLimited = true so AgentRunnerService can suppress re-launches and skip
///   inbox archiving without needing to scan the transcript file after the fact.
/// </summary>
public class ClaudeAgentExecutor(ILogger<ClaudeAgentExecutor> logger) : IAgentExecutor
{
    public string Name => IAgentExecutor.Default;
    public string DisplayName => "Claude CLI";
    public IReadOnlyList<ExecutorSkill> AvailableSkills => ClaudeSkills.All;

    public IReadOnlyList<ModelDescriptor> KnownModels { get; } =
    [
        new("claude-sonnet-4-5",              "Claude Sonnet 4.5",  IAgentExecutor.Default, ModelCapabilities.Streaming | ModelCapabilities.ToolCalling | ModelCapabilities.Vision, MaxTokens: 8192,  ContextWindow: 200_000, InputCostPer1MTokens: 3.00m,  OutputCostPer1MTokens: 15.00m),
        new("claude-sonnet-4-6",              "Claude Sonnet 4.6",  IAgentExecutor.Default, ModelCapabilities.Streaming | ModelCapabilities.ToolCalling | ModelCapabilities.Vision, MaxTokens: 8192,  ContextWindow: 200_000, InputCostPer1MTokens: 3.00m,  OutputCostPer1MTokens: 15.00m),
        new("claude-opus-4-5",               "Claude Opus 4.5",    IAgentExecutor.Default, ModelCapabilities.Streaming | ModelCapabilities.ToolCalling | ModelCapabilities.Vision | ModelCapabilities.Reasoning, MaxTokens: 32_000, ContextWindow: 200_000, InputCostPer1MTokens: 15.00m, OutputCostPer1MTokens: 75.00m),
        new("claude-opus-4-6",               "Claude Opus 4.6",    IAgentExecutor.Default, ModelCapabilities.Streaming | ModelCapabilities.ToolCalling | ModelCapabilities.Vision | ModelCapabilities.Reasoning, MaxTokens: 32_000, ContextWindow: 200_000, InputCostPer1MTokens: 15.00m, OutputCostPer1MTokens: 75.00m),
        new("claude-haiku-4-5-20251001",     "Claude Haiku 4.5",   IAgentExecutor.Default, ModelCapabilities.Streaming | ModelCapabilities.ToolCalling | ModelCapabilities.Vision, MaxTokens: 8192,  ContextWindow: 200_000, InputCostPer1MTokens: 0.80m,  OutputCostPer1MTokens:  4.00m),
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
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            if (OperatingSystem.IsWindows())
            {
                psi.FileName = "cmd.exe";
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add("claude");
            }
            else
            {
                psi.FileName = "claude";
            }
            psi.ArgumentList.Add("--version");

            using var proc = Process.Start(psi);
            if (proc == null)
                return new ExecutorHealthResult(false, "Failed to start claude process");

            var stdout = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);

            var version = stdout.Trim();
            return proc.ExitCode == 0
                ? new ExecutorHealthResult(true, version.Length > 0 ? version : "claude CLI found")
                : new ExecutorHealthResult(false, $"claude --version exited with code {proc.ExitCode}");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new ExecutorHealthResult(false, $"Claude CLI not found: {ex.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // Run
    // -------------------------------------------------------------------------

    public async Task<ExecutorResult> RunAsync(ExecutorContext context, ChannelWriter<string> output)
    {
        var skills = ClaudeSkills.Resolve(context.EnabledSkills);
        var psi = BuildProcessStartInfo(context.WorkingDir, context.ModelId, skills);

        // Inject project secrets as environment variables.
        // Values are sensitive — they are set on the child process environment only; never logged.
        if (context.Secrets is { Count: > 0 } secrets)
        {
            foreach (var (name, value) in secrets)
                psi.Environment[name] = value;
        }

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var isRateLimited = false;
        string? errorMessage = null;
        TokenUsage? capturedUsage = null;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;

            if (!isRateLimited && IsRateLimitLine(e.Data))
            {
                isRateLimited = true;
                logger.LogWarning("[claude] Rate limit detected in output");
            }

            // claude --output-format stream-json emits JSONL. Parse to extract text and usage.
            var (visibleText, usage, failure) = ParseStreamJsonLine(e.Data);

            if (failure is not null)
            {
                logger.LogError("[claude] {FailureMessage}", failure);
                Interlocked.CompareExchange(ref errorMessage, failure, null);
            }
            else
            {
                logger.LogDebug("[claude] {Line}", e.Data);
            }

            if (usage != null)
                Interlocked.Exchange(ref capturedUsage, capturedUsage == null ? usage : capturedUsage + usage);

            // Only write human-readable content — never fall back to raw JSONL.
            if (!string.IsNullOrEmpty(visibleText))
                output.TryWrite($"[{DateTime.UtcNow:o}] {visibleText}");
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;

            if (!isRateLimited && IsRateLimitLine(e.Data))
                isRateLimited = true;

            // Filter known benign warnings that shouldn't be surfaced as errors.
            if (IsBenignStderrLine(e.Data))
            {
                logger.LogDebug("[claude] [stderr/warn] {Line}", e.Data);
                return;
            }

            var failureMessage = $"stderr: {e.Data}";
            logger.LogError("[claude] {FailureMessage}", failureMessage);
            Interlocked.CompareExchange(ref errorMessage, failureMessage, null);
            output.TryWrite($"[{DateTime.UtcNow:o}] [stderr] {e.Data}");
        };

        process.Start();
        context.ReportPid?.Invoke(process.Id);

        // Write the prompt to stdin then close it so the process sees EOF.
        await process.StandardInput.WriteAsync(context.Prompt).ConfigureAwait(false);
        process.StandardInput.Close();

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(context.CancellationToken).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                errorMessage ??= $"Claude exited with code {process.ExitCode}.";
            }

            return new ExecutorResult(process.ExitCode, IsRateLimited: isRateLimited, ErrorMessage: errorMessage, Usage: capturedUsage);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static ProcessStartInfo BuildProcessStartInfo(string workingDir, string modelId, HashSet<string> skills)
    {
        var psi = new ProcessStartInfo
        {
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = System.Text.Encoding.UTF8,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            CreateNoWindow = true,
        };

        // `claude` is an npm global .cmd file on Windows — cannot execute directly
        // when UseShellExecute = false.
        if (OperatingSystem.IsWindows())
        {
            psi.FileName = "cmd.exe";
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add("claude");
        }
        else
        {
            psi.FileName = "claude";
        }

        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add("--verbose");
        psi.ArgumentList.Add("--dangerously-skip-permissions");
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("stream-json");
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(modelId);

        // Translate enabled skills into --allowedTools arguments.
        foreach (var tool in ClaudeSkills.ToAllowedTools(skills))
        {
            psi.ArgumentList.Add("--allowedTools");
            psi.ArgumentList.Add(tool);
        }

        // Force uniformity by denying built-in tools so that Claude MUST fall back to MCP workspace tools
        var disallowedTools = ClaudeSkills.ToDisallowedTools(skills).ToList();
        if (disallowedTools.Count > 0)
        {
            psi.ArgumentList.Add("--disallowedTools");
            psi.ArgumentList.Add(string.Join(",", disallowedTools));
        }

        // If the project has a codebase path, add it as a context directory
        // so its CLAUDE.md (if any) is loaded by the CLI.
        var workspaceRoot = Path.GetFullPath(Path.Combine(workingDir, "..", ".."));
        var codebasePath = ReadCodebasePath(workspaceRoot);
        if (!string.IsNullOrEmpty(codebasePath))
        {
            psi.ArgumentList.Add("--add-dir");
            psi.ArgumentList.Add(codebasePath);
        }

        return psi;
    }

    private static string? ReadCodebasePath(string workspaceRoot)
    {
        var projectJson = Path.Combine(workspaceRoot, "project.json");
        if (!File.Exists(projectJson)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(projectJson));
            return doc.RootElement.TryGetProperty("codebasePath", out var val) ? val.GetString() : null;
        }
        catch { return null; }
    }

    private static bool IsRateLimitLine(string line) =>
        line.Contains("hit your limit", StringComparison.OrdinalIgnoreCase)
        || line.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
        || line.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase)
        || line.Contains("RateLimitError", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true for stderr lines that are informational warnings rather than real errors —
    /// e.g. SSL cert notices from corporate proxies (Zscaler, etc.) that Claude CLI emits on startup.
    /// These should not be surfaced as <c>lastError</c> on the agent.
    /// </summary>
    private static bool IsBenignStderrLine(string line) =>
        line.Contains("ignoring extra certs", StringComparison.OrdinalIgnoreCase)
        || line.Contains("warn:", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Parses a JSONL line from <c>claude --output-format stream-json</c>.
    /// Returns the visible transcript text (if any), token usage (on the result event), and a
    /// failure message (on error events). All three may be null for non-text events.
    /// </summary>
    private static (string? VisibleText, TokenUsage? Usage, string? Failure)
        ParseStreamJsonLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return (null, null, null);

        // Non-JSON lines (plain text progress) — pass through as-is.
        if (!line.TrimStart().StartsWith('{')) return (line, null, null);

        JsonElement root;
        try   { root = JsonDocument.Parse(line).RootElement; }
        catch { return (line, null, null); }

        var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;

        switch (type)
        {
            case "assistant":
            {
                // Extract text and tool-call descriptions from content blocks.
                if (!root.TryGetProperty("message", out var msg)) break;
                if (!msg.TryGetProperty("content", out var content)) break;
                var sb = new System.Text.StringBuilder();
                foreach (var block in content.EnumerateArray())
                {
                    if (!block.TryGetProperty("type", out var bt)) continue;
                    var blockType = bt.GetString();

                    if (blockType == "text" && block.TryGetProperty("text", out var txt))
                    {
                        sb.Append(txt.GetString());
                    }
                    else if (blockType == "tool_use")
                    {
                        var toolName = block.TryGetProperty("name", out var tn) ? tn.GetString() : "tool";
                        var desc = block.TryGetProperty("input", out var inp)
                            && inp.TryGetProperty("description", out var d) ? d.GetString() : null;
                        if (sb.Length > 0) sb.AppendLine();
                        sb.Append($"▶ {toolName}");
                        if (!string.IsNullOrEmpty(desc)) sb.Append($": {desc}");
                    }
                }
                return (sb.Length > 0 ? sb.ToString() : null, null, null);
            }

            case "system":
            {
                // task_progress events carry a human-readable description of the running sub-task.
                var subtype = root.TryGetProperty("subtype", out var st) ? st.GetString() : null;
                if (subtype == "task_progress" && root.TryGetProperty("description", out var desc))
                {
                    var text = desc.GetString();
                    return (!string.IsNullOrEmpty(text) ? $"⟳ {text}" : null, null, null);
                }
                return (null, null, null);
            }

            case "result":
            {
                TokenUsage? usage = null;
                if (root.TryGetProperty("usage", out var u))
                {
                    var input  = u.TryGetProperty("input_tokens",  out var it) ? it.GetInt64() : 0;
                    var output = u.TryGetProperty("output_tokens", out var ot) ? ot.GetInt64() : 0;
                    if (input > 0 || output > 0)
                        usage = new TokenUsage(input, output);
                }
                return (null, usage, null);
            }

            case "error":
            {
                var msg = root.TryGetProperty("error", out var err)
                    ? (err.TryGetProperty("message", out var m) ? m.GetString() : err.ToString())
                    : line;
                return (null, null, msg ?? line);
            }
        }

        // user/tool_result/rate_limit_event and other events — nothing to emit.
        return (null, null, null);
    }

}
