namespace AiDev.Executors;

/// <summary>
/// Runs agents via the Claude CLI (`claude -p --model &lt;id&gt; --allowedTools ...`).
/// The prompt is written to stdin to avoid Windows command-line length and newline-escaping issues.
/// On Windows, wraps the call in `cmd.exe /c` because the `claude` npm global is a .cmd file.
///
/// Permission model (safest-first):
///   1. Default: --allowedTools scopes Read/Write/Edit to the workspace root (2 levels up from agentDir)
///      and allows read-only git inspection. Nothing outside the workspace can be written.
///   2. Fallback: set CLAUDE_DANGEROUSLY_SKIP_PERMISSIONS=1 to bypass all controls (dev only).
/// </summary>
public class ClaudeAgentExecutor(ILogger<ClaudeAgentExecutor> logger) : IAgentExecutor
{
    public string Name => IAgentExecutor.Default;

    public async Task<int> RunAsync(string workingDir, string modelId, string prompt,
        ChannelWriter<string> output, Action<int>? reportPid, CancellationToken ct)
    {
        var psi = BuildProcessStartInfo(workingDir, modelId);
        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            logger.LogDebug("[claude] {Line}", e.Data);
            output.TryWrite($"[{DateTime.UtcNow:o}] {e.Data}");
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            logger.LogWarning("[claude] stderr: {Data}", e.Data);
            output.TryWrite($"[{DateTime.UtcNow:o}] [stderr] {e.Data}");
        };

        process.Start();
        reportPid?.Invoke(process.Id);

        // Write the prompt to stdin then close it so the process sees EOF.
        // This avoids Windows cmd.exe command-line length limits and newline-escaping issues
        // that occur when the prompt is passed as a quoted argument.
        await process.StandardInput.WriteAsync(prompt);
        process.StandardInput.Close();

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            throw; // re-throw so AgentRunnerService can handle as cancellation (exit code 130)
        }
    }

    private static ProcessStartInfo BuildProcessStartInfo(string workingDir, string modelId)
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

        // `claude` is a npm global .cmd file on Windows — can't execute directly
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

        if (Environment.GetEnvironmentVariable("CLAUDE_DANGEROUSLY_SKIP_PERMISSIONS") == "1")
        {
            // Full bypass — dev/testing only.
            psi.ArgumentList.Add("--dangerously-skip-permissions");
        }
        else
        {
            // Scope permissions to the workspace root (2 levels up: agents/{slug}/ → project root)
            // plus the project's configured codebase path (read from project.json).
            // Each rule is a separate argument.
            var workspaceRoot = Path.GetFullPath(Path.Combine(workingDir, "..", ".."));
            var workspaceGlob = ToGlob(workspaceRoot);

            psi.ArgumentList.Add("--allowedTools");
            psi.ArgumentList.Add($"Read(*)");
            psi.ArgumentList.Add($"Write({workspaceGlob})");
            psi.ArgumentList.Add($"Edit({workspaceGlob})");

            // If the project has a codebase path configured, grant read/write there too
            // and add it as an extra context directory so its CLAUDE.md (if any) is loaded.
            var codebasePath = ReadCodebasePath(workspaceRoot);
            if (!string.IsNullOrEmpty(codebasePath))
            {
                var codebaseGlob = ToGlob(codebasePath);
                psi.ArgumentList.Add($"Write({codebaseGlob})");
                psi.ArgumentList.Add($"Edit({codebaseGlob})");
                psi.ArgumentList.Add("--add-dir");
                psi.ArgumentList.Add(codebasePath);
            }

            psi.ArgumentList.Add("Bash(git log *)");
            psi.ArgumentList.Add("Bash(git diff *)");
            psi.ArgumentList.Add("Bash(git add *)");
            psi.ArgumentList.Add("Bash(git commit *)");
            psi.ArgumentList.Add("Bash(git status)");
        }

        return psi;
    }

    private static string ToGlob(string path) =>
        Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/') + "/**";

    private static string? ReadCodebasePath(string workspaceRoot)
    {
        var projectJson = Path.Combine(workspaceRoot, "project.json");
        if (!File.Exists(projectJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(projectJson));
            return doc.RootElement.TryGetProperty("codebasePath", out var val) ? val.GetString() : null;
        }
        catch { return null; }
    }
}
