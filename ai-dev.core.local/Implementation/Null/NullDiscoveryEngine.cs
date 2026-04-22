using AiDev.Core.Local.Contracts;

namespace AiDev.Core.Local.Implementation.Null;

internal sealed class NullDiscoveryEngine : IProgressiveDiscoveryEngine
{
    public Task<Result<DiscoveryBatch>> DiscoverAsync(DiscoveryRequest request, CancellationToken ct = default)
        => Task.FromResult<Result<DiscoveryBatch>>(new Ok<DiscoveryBatch>(
            new DiscoveryBatch(
                Slices: [],
                Synthesis: string.Empty,
                Confidence: 0m,
                RecommendedNextStep: string.Empty)));
}
