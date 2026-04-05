namespace AiDev.Executors;

/// <summary>
/// Runs agents via the Claude CLI (`claude -p --model &lt;id&gt; --allowedTools ...`).
/// The prompt is written to stdin to avoid Windows command-line length and newline-escaping issues.
/// On Windows, wraps the call in `cmd.exe /c` because the `claude` npm global is a .cmd file.
///
/// Permission model:
///   Agents connect to the ai-dev.mcp MCP server registered in .claude/settings.json.
///   The MCP server enforces path boundaries and logs all operations. Raw Read/Write/Edit/Bash
///   tools are denied by settings.json — agents can only use workspace operations exposed by the
///   MCP server. The --allowedTools flag grants git inspection commands directly.
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

        // MCP server (registered in .claude/settings.json) handles workspace I/O.
        // Only grant git inspection and commit tools directly.
        psi.ArgumentList.Add("--allowedTools");
        psi.ArgumentList.Add("Bash(git log *)");
        psi.ArgumentList.Add("Bash(git diff *)");
        psi.ArgumentList.Add("Bash(git add *)");
        psi.ArgumentList.Add("Bash(git commit *)");
        psi.ArgumentList.Add("Bash(git status)");

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
            using var doc = JsonDocument.Parse(File.ReadAllText(projectJson));
            return doc.RootElement.TryGetProperty("codebasePath", out var val) ? val.GetString() : null;
        }
        catch { return null; }
    }
}
