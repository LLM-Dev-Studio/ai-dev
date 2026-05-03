using AiDev.Core.Local.Contracts;
using AiDev.Core.Local.Implementation.Null;

namespace AiDevNet.Tests.Unit;

public class NullImplementationTests
{
    private static readonly RuntimeModelProfile AnyProfile = new(
        ModelId: "any", ModelClass: "large-local", Provider: "ollama",
        MaxInputTokens: 32_000, SupportsParallelTools: false);

    private static readonly LocalRuntimeState AnyState = new(
        Objective: new LocalObjective("goal", null, null, Guid.NewGuid()),
        Transcript: new RuntimeTranscript([], []),
        Budget: new RuntimeBudget(50, 10, 3, 32_000),
        ModelProfile: AnyProfile,
        StartedAtUtc: DateTimeOffset.UtcNow,
        Iteration: 0);

    [Fact]
    public async Task NullPlanner_ReturnsOkWithEmptyToolRequests()
    {
        var result = await new NullPlanner().PlanNextAsync(AnyState, TestContext.Current.CancellationToken);
        var plan = result.ShouldBeOfType<Ok<RuntimeActionPlan>>().Value;
        plan.ToolRequests.ShouldBeEmpty();
        plan.RequiresUserInput.ShouldBeFalse();
    }

    [Fact]
    public async Task NullToolBroker_ReturnsOkWithEmptyOutcomes()
    {
        var result = await new NullToolBroker().ExecuteAsync([], TestContext.Current.CancellationToken);
        result.ShouldBeOfType<Ok<IReadOnlyList<ToolOutcome>>>().Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task NullDiscoveryEngine_ReturnsOkWithEmptyBatch()
    {
        var request = new DiscoveryRequest("query", [], 5, true);
        var result = await new NullDiscoveryEngine().DiscoverAsync(request, TestContext.Current.CancellationToken);
        var batch = result.ShouldBeOfType<Ok<DiscoveryBatch>>().Value;
        batch.Slices.ShouldBeEmpty();
        batch.Confidence.ShouldBe(0m);
    }

    [Fact]
    public void NullCompactor_ReturnsOkWithEmptySnapshot()
    {
        var result = new NullCompactor().Compact(AnyState);
        var snapshot = result.ShouldBeOfType<Ok<CompactionSnapshot>>().Value;
        snapshot.Facts.ShouldBeEmpty();
        snapshot.EstimatedTokens.ShouldBe(0);
    }

    [Fact]
    public void NullModelStrategyResolver_ReturnsOkWithSafeDefaults()
    {
        var result = new NullModelStrategyResolver().Resolve(AnyProfile);
        var strategy = result.ShouldBeOfType<Ok<RuntimeModelStrategy>>().Value;
        strategy.MaxParallelTools.ShouldBe(1);
        strategy.PlanningDepth.ShouldBe(1);
    }
}
