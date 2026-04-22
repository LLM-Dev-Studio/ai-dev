using AiDev.Core.Local.Contracts;
using AiDev.Core.Local.Implementation;

namespace AiDevNet.Tests.Unit;

public class StaticModelStrategyResolverTests
{
    public static TheoryData<string, int, int, int, decimal, int> ModelClassTable => new()
    {
        { "small-local", 1, 3, 1, 0.6m, 3 },
        { "large-local", 3, 8, 4, 0.5m, 5 },
    };

    [Theory]
    [MemberData(nameof(ModelClassTable))]
    public void Resolve_KnownModelClass_ReturnsExpectedStrategy(
        string modelClass,
        int planningDepth,
        int discoveryBreadth,
        int maxParallelTools,
        decimal minimumConfidence,
        int compactionInterval)
    {
        var profile = new RuntimeModelProfile("test-model", modelClass, "ollama", 32_000, false);

        var result = new StaticModelStrategyResolver().Resolve(profile);

        var strategy = result.ShouldBeOfType<Ok<RuntimeModelStrategy>>().Value;
        strategy.PlanningDepth.ShouldBe(planningDepth);
        strategy.DiscoveryBreadth.ShouldBe(discoveryBreadth);
        strategy.MaxParallelTools.ShouldBe(maxParallelTools);
        strategy.MinimumConfidenceToProceed.ShouldBe(minimumConfidence);
        strategy.CompactionInterval.ShouldBe(compactionInterval);
    }

    [Theory]
    [InlineData("SMALL-LOCAL")]
    [InlineData("Large-Local")]
    public void Resolve_ModelClassIsCaseInsensitive_ReturnsOk(string modelClass)
    {
        var profile = new RuntimeModelProfile("test-model", modelClass, "ollama", 32_000, false);

        var result = new StaticModelStrategyResolver().Resolve(profile);

        result.ShouldBeOfType<Ok<RuntimeModelStrategy>>();
    }

    [Fact]
    public void Resolve_UnknownModelClass_ReturnsErrWithCode()
    {
        var profile = new RuntimeModelProfile("test-model", "unknown-class", "ollama", 32_000, false);

        var result = new StaticModelStrategyResolver().Resolve(profile);

        var err = result.ShouldBeOfType<Err<RuntimeModelStrategy>>();
        err.Error.Code.ShouldBe("ModelStrategy.UnknownModelClass");
    }
}
