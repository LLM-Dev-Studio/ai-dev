namespace AiDev.Executors;

/// <summary>
/// Runs agents via the Claude CLI (`claude -p &lt;prompt&gt; --model &lt;id&gt; --dangerously-skip-permissions`).
/// On Windows, wraps the call in `cmd.exe /c` because the `claude` npm global is a .cmd file.
/// </summary>
public class ClaudeAgentExecutor(ILogger<ClaudeAgentExecutor> logger) : IAgentExecutor
{
    public string Name => IAgentExecutor.Default;

    public async Task<int> RunAsync(string workingDir, string modelId, string prompt,
        ChannelWriter<string> output, Action<int>? reportPid, CancellationToken ct)
    {
        var psi = BuildProcessStartInfo(workingDir, modelId, prompt);
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

    private static ProcessStartInfo BuildProcessStartInfo(string workingDir, string modelId, string prompt)
    {
        var psi = new ProcessStartInfo
        {
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
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
        psi.ArgumentList.Add(prompt);
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(modelId);

        // SECURITY: --dangerously-skip-permissions grants unrestricted filesystem/shell access.
        // Enabled only when CLAUDE_DANGEROUSLY_SKIP_PERMISSIONS=1 is explicitly set.
        // Set this env var in development; leave it unset in restricted or multi-tenant deployments.
        if (Environment.GetEnvironmentVariable("CLAUDE_DANGEROUSLY_SKIP_PERMISSIONS") == "1")
            psi.ArgumentList.Add("--dangerously-skip-permissions");

        return psi;
    }
}
