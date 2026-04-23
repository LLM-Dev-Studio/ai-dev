using AiDev.Core.Local.Contracts;

namespace AiDev.Core.Local.Implementation;

internal sealed class StaticModelStrategyResolver : IModelStrategyResolver
{
    private static readonly Dictionary<string, RuntimeModelStrategy> Strategies = new(StringComparer.OrdinalIgnoreCase)
    {
        // Conservative policy — compact aggressively because context windows are smaller.
        ["small-local"] = new RuntimeModelStrategy(
            PlanningDepth: 1,
            DiscoveryBreadth: 3,
            MaxParallelTools: 1,
            MinimumConfidenceToProceed: 0.6m,
            CompactionInterval: 3,
            CompactionRatio: 0.6m,
            ToolCallBudget: 20),

        // Relaxed policy — larger models can hold more context before compaction is needed.
        ["large-local"] = new RuntimeModelStrategy(
            PlanningDepth: 3,
            DiscoveryBreadth: 8,
            MaxParallelTools: 4,
            MinimumConfidenceToProceed: 0.5m,
            CompactionInterval: 5,
            CompactionRatio: 0.8m,
            ToolCallBudget: 50),
    };

    public Result<RuntimeModelStrategy> Resolve(RuntimeModelProfile profile)
    {
        if (Strategies.TryGetValue(profile.ModelClass, out var strategy))
            return new Ok<RuntimeModelStrategy>(strategy);

        return new Err<RuntimeModelStrategy>(new DomainError(
            "ModelStrategy.UnknownModelClass",
            $"No strategy defined for model class '{profile.ModelClass}'."));
    }
}
