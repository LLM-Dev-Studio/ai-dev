using AiDev.Core.Local.Contracts;

namespace AiDev.Core.Local.Implementation.Null;

internal sealed class NullToolBroker : ILocalToolBroker
{
    public Task<Result<IReadOnlyList<ToolOutcome>>> ExecuteAsync(
        IReadOnlyList<ToolRequest> requests,
        CancellationToken ct = default)
        => Task.FromResult<Result<IReadOnlyList<ToolOutcome>>>(new Ok<IReadOnlyList<ToolOutcome>>([]));
}
