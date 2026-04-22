using AiDev.Core.Local.Contracts;
using AiDev.Core.Local.Implementation;

namespace AiDevNet.Tests.Unit;

public class FileSystemRuntimeMemoryStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private FileSystemRuntimeMemoryStore CreateStore() => new(_tempDir);

    [Fact]
    public async Task SaveThenLoad_ReturnsSavedFacts()
    {
        var store = CreateStore();
        var id = Guid.NewGuid();
        var snapshot = new CompactionSnapshot(
            CompactSummary: "summary",
            Facts: [new RuntimeFact("code", "IAgentExecutor found", ["file.cs:10", "file.cs:20"], true)],
            OpenQuestions: ["Should we add caching?"],
            EstimatedTokens: 50);

        await store.SaveSnapshotAsync(id, snapshot, TestContext.Current.CancellationToken);
        var result = await store.LoadFactsAsync(id, TestContext.Current.CancellationToken);

        var facts = result.ShouldBeOfType<Ok<IReadOnlyList<RuntimeFact>>>().Value;
        facts.Count.ShouldBe(1);
        facts[0].Fact.ShouldBe("IAgentExecutor found");
        facts[0].IsStable.ShouldBeTrue();
    }

    [Fact]
    public async Task LoadFacts_ForUnknownObjective_ReturnsEmptyList()
    {
        var store = CreateStore();

        var result = await store.LoadFactsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Ok<IReadOnlyList<RuntimeFact>>>().Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveSnapshot_WritesFileUnderMemoryDir()
    {
        var store = CreateStore();
        var id = Guid.NewGuid();
        var snapshot = new CompactionSnapshot("s", [], [], 0);

        await store.SaveSnapshotAsync(id, snapshot, TestContext.Current.CancellationToken);

        var expectedFile = Path.Combine(_tempDir, $"{id:N}.json");
        File.Exists(expectedFile).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveSnapshot_OverwritesPreviousSnapshot()
    {
        var store = CreateStore();
        var id = Guid.NewGuid();

        var first = new CompactionSnapshot("first", [new RuntimeFact("a", "fact a", [], true)], [], 10);
        var second = new CompactionSnapshot("second", [new RuntimeFact("b", "fact b", [], true)], [], 20);

        await store.SaveSnapshotAsync(id, first, TestContext.Current.CancellationToken);
        await store.SaveSnapshotAsync(id, second, TestContext.Current.CancellationToken);

        var result = await store.LoadFactsAsync(id, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<Ok<IReadOnlyList<RuntimeFact>>>().Value.Single().Fact.ShouldBe("fact b");
    }

    [Fact]
    public async Task LoadFacts_PersistsAcrossStoreInstances()
    {
        var id = Guid.NewGuid();
        var snapshot = new CompactionSnapshot("s", [new RuntimeFact("c", "durable fact", [], false)], [], 5);

        await CreateStore().SaveSnapshotAsync(id, snapshot, TestContext.Current.CancellationToken);
        var result = await CreateStore().LoadFactsAsync(id, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Ok<IReadOnlyList<RuntimeFact>>>().Value.Single().Fact.ShouldBe("durable fact");
    }
}
