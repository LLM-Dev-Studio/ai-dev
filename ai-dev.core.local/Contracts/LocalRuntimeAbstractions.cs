using AiDev.Models;

namespace AiDev.Core.Local.Contracts;

public interface ILocalPlanner
{
    Task<Result<RuntimeActionPlan>> PlanNextAsync(LocalRuntimeState state, CancellationToken ct = default);
}

public interface ILocalToolBroker
{
    Task<Result<IReadOnlyList<ToolOutcome>>> ExecuteAsync(
        IReadOnlyList<ToolRequest> requests,
        CancellationToken ct = default);
}

public interface IProgressiveDiscoveryEngine
{
    Task<Result<DiscoveryBatch>> DiscoverAsync(DiscoveryRequest request, CancellationToken ct = default);
}

public interface IContextCompactor
{
    Result<CompactionSnapshot> Compact(LocalRuntimeState state);
}

public interface IRuntimeMemoryStore
{
    Task<Result<Unit>> SaveSnapshotAsync(Guid objectiveId, CompactionSnapshot snapshot, CancellationToken ct = default);
    Task<Result<IReadOnlyList<RuntimeFact>>> LoadFactsAsync(Guid objectiveId, CancellationToken ct = default);
}

public interface IModelStrategyResolver
{
    Result<RuntimeModelStrategy> Resolve(RuntimeModelProfile profile);
}

public interface ILlmClient
{
    string Provider { get; }
    Task<Result<string>> CompleteAsync(string prompt, string modelId, CancellationToken ct = default);
}

public interface ILocalOrchestrator
{
    Task<Result<CompactionSnapshot>> RunAsync(
        LocalObjective objective,
        RuntimeModelProfile modelProfile,
        CancellationToken ct = default);
}

public sealed record RuntimeModelStrategy(
    int PlanningDepth,
    int DiscoveryBreadth,
    int MaxParallelTools,
    decimal MinimumConfidenceToProceed,
    int CompactionInterval);
