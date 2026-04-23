using AiDev.Core.Local.Contracts;

namespace AiDev.Core.Local.Implementation.Null;

internal sealed class NullModelStrategyResolver : IModelStrategyResolver
{
    private static readonly RuntimeModelStrategy SafeDefaults = new(
        PlanningDepth: 1,
        DiscoveryBreadth: 3,
        MaxParallelTools: 1,
        MinimumConfidenceToProceed: 0.5m,
        CompactionInterval: 5);

    public Result<RuntimeModelStrategy> Resolve(RuntimeModelProfile profile)
        => new Ok<RuntimeModelStrategy>(SafeDefaults);
}
