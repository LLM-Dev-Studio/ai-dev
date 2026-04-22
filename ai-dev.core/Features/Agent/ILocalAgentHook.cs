using System.Threading.Channels;

namespace AiDev.Features.Agent;

public interface ILocalAgentHook
{
    bool IsApplicable(string executorName);

    Task<LocalAgentHookResult> RunAsync(
        LocalAgentHookContext context,
        ChannelWriter<string> output,
        CancellationToken ct);
}

public sealed record LocalAgentHookContext(
    string Goal,
    string WorkingDir,
    string ModelId,
    string ExecutorName,
    Guid SessionId);

public sealed record LocalAgentHookResult(
    bool Succeeded,
    string? ErrorMessage = null);
