using AiDev.Core.Local.Contracts;
using AiDev.Core.Local.Implementation;

namespace AiDevNet.Tests.Unit;

public class ProgressiveDiscoveryEngineTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public ProgressiveDiscoveryEngineTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private ProgressiveDiscoveryEngine Engine() => new(_root);

    private string WriteFile(string relativePath, string content)
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    [Fact]
    public async Task DiscoverAsync_WhenNoFilesPresent_ReturnsZeroConfidence()
    {
        var request = new DiscoveryRequest("IAgentExecutor", [], 5, false);

        var result = await Engine().DiscoverAsync(request, TestContext.Current.CancellationToken);

        var batch = result.ShouldBeOfType<Ok<DiscoveryBatch>>().Value;
        batch.Slices.ShouldBeEmpty();
        batch.Confidence.ShouldBe(0m);
    }

    [Fact]
    public async Task DiscoverAsync_WhenQueryMatchesFile_ReturnsSliceWithEvidence()
    {
        WriteFile("src/AgentService.cs", """
            namespace AiDev;
            public interface IAgentExecutor
            {
                Task RunAsync();
            }
            """);

        var request = new DiscoveryRequest("IAgentExecutor", [], 5, true);

        var batch = (await Engine().DiscoverAsync(request, TestContext.Current.CancellationToken))
            .ShouldBeOfType<Ok<DiscoveryBatch>>().Value;

        batch.Slices.Count.ShouldBe(1);
        batch.Slices[0].Evidence.ShouldNotBeEmpty();
        batch.Confidence.ShouldBeGreaterThan(0m);
    }

    [Fact]
    public async Task DiscoverAsync_WithExplicitCandidatePaths_SearchesOnlyThoseFiles()
    {
        var matching = WriteFile("src/Executor.cs", "public interface IAgentExecutor { }");
        WriteFile("src/Other.cs", "// unrelated content");

        var request = new DiscoveryRequest("IAgentExecutor", [matching], 5, false);

        var batch = (await Engine().DiscoverAsync(request, TestContext.Current.CancellationToken))
            .ShouldBeOfType<Ok<DiscoveryBatch>>().Value;

        batch.Slices.Count.ShouldBe(1);
        batch.Slices[0].Path.ShouldBe(matching);
    }

    [Fact]
    public async Task DiscoverAsync_RespectsMaxSlices()
    {
        for (var i = 0; i < 5; i++)
            WriteFile($"src/File{i}.cs", $"public class Service{i} {{ IAgentExecutor executor; }}");

        var request = new DiscoveryRequest("IAgentExecutor", [], MaxSlices: 3, RestrictToCodeFiles: true);

        var batch = (await Engine().DiscoverAsync(request, TestContext.Current.CancellationToken))
            .ShouldBeOfType<Ok<DiscoveryBatch>>().Value;

        batch.Slices.Count.ShouldBeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task DiscoverAsync_RestrictToCodeFiles_IgnoresNonCodeFiles()
    {
        WriteFile("notes.txt", "IAgentExecutor is important");
        WriteFile("src/Executor.cs", "public interface IAgentExecutor { }");

        var request = new DiscoveryRequest("IAgentExecutor", [], 5, RestrictToCodeFiles: true);

        var batch = (await Engine().DiscoverAsync(request, TestContext.Current.CancellationToken))
            .ShouldBeOfType<Ok<DiscoveryBatch>>().Value;

        batch.Slices.ShouldAllBe(s => s.Path.EndsWith(".cs"));
    }

    [Fact]
    public async Task DiscoverAsync_SliceContainsLineNumbers()
    {
        WriteFile("src/Contract.cs", string.Join(Environment.NewLine, Enumerable.Range(1, 30).Select(i =>
            i == 15 ? "public interface IAgentExecutor { }" : $"// line {i}")));

        var request = new DiscoveryRequest("IAgentExecutor", [], 5, true);

        var batch = (await Engine().DiscoverAsync(request, TestContext.Current.CancellationToken))
            .ShouldBeOfType<Ok<DiscoveryBatch>>().Value;

        var slice = batch.Slices.Single();
        slice.StartLine.ShouldBeGreaterThan(0);
        slice.EndLine.ShouldBeGreaterThanOrEqualTo(slice.StartLine);
    }

    [Fact]
    public async Task DiscoverAsync_IgnoresBinAndObjDirectories()
    {
        WriteFile("obj/Debug/Generated.cs", "IAgentExecutor placeholder");
        WriteFile("src/Real.cs", "public interface IAgentExecutor { }");

        var request = new DiscoveryRequest("IAgentExecutor", [], 5, true);

        var batch = (await Engine().DiscoverAsync(request, TestContext.Current.CancellationToken))
            .ShouldBeOfType<Ok<DiscoveryBatch>>().Value;

        batch.Slices.ShouldAllBe(s => !s.Path.Contains("obj"));
    }
}
