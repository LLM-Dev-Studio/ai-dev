using AiDev.Core.Local.Contracts;
using AiDev.Core.Local.Implementation;

namespace AiDevNet.Tests.Unit;

public class InMemoryRuntimeMemoryStoreTests
{
    [Fact]
    public async Task SaveThenLoad_ReturnsSavedFacts()
    {
        var store = new InMemoryRuntimeMemoryStore();
        var id = Guid.NewGuid();
        var snapshot = new CompactionSnapshot(
            CompactSummary: "summary",
            Facts: [new RuntimeFact("code", "IAgentExecutor found", ["file.cs:10"], true)],
            OpenQuestions: [],
            EstimatedTokens: 100);

        await store.SaveSnapshotAsync(id, snapshot, TestContext.Current.CancellationToken);
        var result = await store.LoadFactsAsync(id, TestContext.Current.CancellationToken);

        var facts = result.ShouldBeOfType<Ok<IReadOnlyList<RuntimeFact>>>().Value;
        facts.Count.ShouldBe(1);
        facts[0].Fact.ShouldBe("IAgentExecutor found");
    }

    [Fact]
    public async Task LoadFacts_ForUnknownObjective_ReturnsEmptyList()
    {
        var store = new InMemoryRuntimeMemoryStore();

        var result = await store.LoadFactsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        var facts = result.ShouldBeOfType<Ok<IReadOnlyList<RuntimeFact>>>().Value;
        facts.ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveSnapshot_OverwritesPreviousSnapshot()
    {
        var store = new InMemoryRuntimeMemoryStore();
        var id = Guid.NewGuid();

        var first = new CompactionSnapshot("first", [new RuntimeFact("a", "fact a", [], true)], [], 10);
        var second = new CompactionSnapshot("second", [new RuntimeFact("b", "fact b", [], true)], [], 20);

        await store.SaveSnapshotAsync(id, first, TestContext.Current.CancellationToken);
        await store.SaveSnapshotAsync(id, second, TestContext.Current.CancellationToken);

        var result = await store.LoadFactsAsync(id, TestContext.Current.CancellationToken);
        var facts = result.ShouldBeOfType<Ok<IReadOnlyList<RuntimeFact>>>().Value;
        facts.Single().Fact.ShouldBe("fact b");
    }
}
