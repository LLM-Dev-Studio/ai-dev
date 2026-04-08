using System.Diagnostics;
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
        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var isRateLimited = false;
        string? errorMessage = null;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;

            var failureMessage = TryGetFailureMessage(e.Data);
            if (failureMessage is not null)
            {
                logger.LogError("[claude] {FailureMessage}", failureMessage);
                Interlocked.CompareExchange(ref errorMessage, failureMessage, null);
            }
            else
            {
                logger.LogDebug("[claude] {Line}", e.Data);
            }

            if (!isRateLimited && IsRateLimitLine(e.Data))
            {
                isRateLimited = true;
                logger.LogWarning("[claude] Rate limit detected in output");
            }
            output.TryWrite($"[{DateTime.UtcNow:o}] {e.Data}");
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;

            var failureMessage = TryGetFailureMessage(e.Data) ?? $"stderr: {e.Data}";
            logger.LogError("[claude] {FailureMessage}", failureMessage);
            Interlocked.CompareExchange(ref errorMessage, failureMessage, null);

            if (!isRateLimited && IsRateLimitLine(e.Data))
                isRateLimited = true;
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

            return new ExecutorResult(process.ExitCode, IsRateLimited: isRateLimited, ErrorMessage: errorMessage);
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

    private static string? TryGetFailureMessage(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        return line.Contains("API Error:", StringComparison.OrdinalIgnoreCase)
            || line.Contains("\"type\":\"error\"", StringComparison.OrdinalIgnoreCase)
            || line.Contains("[error]", StringComparison.OrdinalIgnoreCase)
            ? line
            : null;
    }
}
