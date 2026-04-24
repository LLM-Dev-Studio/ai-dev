using AiDev.Features.Agent;

using System.Threading.Channels;

namespace AiDev.Core.Local.Orchestration;

internal sealed class LocalAgentHookAdapter(ILocalOrchestrator orchestrator) : ILocalAgentHook
{
    private static readonly HashSet<string> LocalExecutors =
        new(StringComparer.OrdinalIgnoreCase) { "ollama", "lmstudio" };

    public bool IsApplicable(string executorName) => LocalExecutors.Contains(executorName);

    public async Task<LocalAgentHookResult> RunAsync(
        LocalAgentHookContext context,
        ChannelWriter<string> output,
        CancellationToken ct)
    {
        var objective = new LocalObjective(
            Goal: context.Goal,
            SuccessCriteria: null,
            CodebaseRoot: context.WorkingDir,
            CorrelationId: context.SessionId);

        var modelProfile = new RuntimeModelProfile(
            ModelId: context.ModelId,
            ModelClass: "large-local",
            Provider: context.ExecutorName,
            MaxInputTokens: 32_000,
            SupportsParallelTools: false);

        var result = await orchestrator.RunAsync(objective, modelProfile, ct).ConfigureAwait(false);

        return result switch
        {
            Ok<CompactionSnapshot> ok => new LocalAgentHookResult(Succeeded: true),
            Err<CompactionSnapshot> err => new LocalAgentHookResult(
                Succeeded: false,
                ErrorMessage: err.Error.Message),
            _ => new LocalAgentHookResult(Succeeded: false, ErrorMessage: "Unexpected orchestrator result."),
        };
    }
}
