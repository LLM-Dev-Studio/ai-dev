using System.Text;
using System.Threading.Channels;

using AiDev.Features.Planning;
using AiDev.Features.Planning.Models;
using AiDev.Models.Types;

namespace AiDev.Executors;

/// <summary>
/// Claude CLI implementation of <see cref="IPlanningLlmClient"/>.
/// Uses the existing <see cref="ClaudeAgentExecutor"/> so planning follows the analyst's assigned executor.
/// </summary>
public sealed class ClaudePlanningLlmClient(ClaudeAgentExecutor executor) : IPlanningLlmClient
{
    public string ExecutorName => AgentExecutorName.ClaudeValue;

    public async Task<PlanningLlmResponse> ChatAsync(
        string modelId,
        string systemPrompt,
        IReadOnlyList<ConversationMessage> history,
        string userMessage,
        CancellationToken ct = default)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ai-planning-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(tempRoot, "workspaces");
        const string projectSlug = "_planning";
        var workingDir = Path.Combine(workspaceRoot, projectSlug, "agents", "planning");

        try
        {
            Directory.CreateDirectory(workingDir);
            await File.WriteAllTextAsync(Path.Combine(workingDir, "CLAUDE.md"), systemPrompt, ct).ConfigureAwait(false);

            var prompt = BuildPrompt(history, userMessage);
            var outputLines = new List<string>();
            var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
            var consumer = Task.Run(async () =>
            {
                await foreach (var line in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                    outputLines.Add(line);
            }, ct);

            var context = new ExecutorContext(
                WorkspaceRoot: workspaceRoot,
                ProjectSlug: projectSlug,
                WorkingDir: workingDir,
                ModelId: modelId,
                Prompt: prompt,
                CancellationToken: ct,
                EnabledSkills: [],
                ReportPid: null,
                Trigger: null,
                Secrets: null,
                ThinkingLevel: ThinkingLevel.Off);

            ExecutorResult result;
            try
            {
                result = await executor.RunAsync(context, channel.Writer).ConfigureAwait(false);
            }
            finally
            {
                channel.Writer.TryComplete();
                await consumer.ConfigureAwait(false);
            }

            if (result.ExitCode != 0)
                throw new InvalidOperationException(result.ErrorMessage ?? "Claude planning call failed.");

            var content = ExtractVisibleText(outputLines);
            var inputTokens = result.Usage is null ? 0 : ToInt(result.Usage.InputTokens);
            var outputTokens = result.Usage is null ? 0 : ToInt(result.Usage.OutputTokens);

            return new PlanningLlmResponse(content, inputTokens, outputTokens);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    private static string BuildPrompt(IReadOnlyList<ConversationMessage> history, string userMessage)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Continue this conversation and answer as the assistant.");
        sb.AppendLine("Use the conversation context and respond directly to the user's latest message.");
        sb.AppendLine();

        foreach (var message in history)
        {
            var role = string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                ? "ASSISTANT"
                : "USER";
            sb.Append(role).Append(": ").AppendLine(message.Content);
        }

        sb.Append("USER: ").AppendLine(userMessage);
        sb.Append("ASSISTANT: ");
        return sb.ToString();
    }

    private static string ExtractVisibleText(IEnumerable<string> lines)
    {
        var sb = new StringBuilder();

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r', '\n');
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var content = line;
            if (line.StartsWith('['))
            {
                var closeTs = line.IndexOf(']');
                if (closeTs > 0 && closeTs + 2 <= line.Length)
                    content = line[(closeTs + 2)..];
            }

            if (content.StartsWith("[error]", StringComparison.OrdinalIgnoreCase)
                || content.StartsWith("[stderr]", StringComparison.OrdinalIgnoreCase)
                || content.StartsWith("[stall-check]", StringComparison.OrdinalIgnoreCase)
                || content.StartsWith("[claude]", StringComparison.OrdinalIgnoreCase)
                || content.StartsWith("▶", StringComparison.Ordinal)
                || content.StartsWith("⟳", StringComparison.Ordinal))
            {
                continue;
            }

            sb.Append(content);
        }

        return sb.ToString().Trim();
    }

    private static int ToInt(long value) => value > int.MaxValue ? int.MaxValue : (int)value;
}
