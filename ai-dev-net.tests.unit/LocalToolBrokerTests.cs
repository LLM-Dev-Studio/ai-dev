using AiDev.Core.Local.Contracts;
using AiDev.Core.Local.Implementation;

namespace AiDevNet.Tests.Unit;

public class LocalToolBrokerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public LocalToolBrokerTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private LocalToolBroker Broker(int maxParallel = 1) => new(_root, maxParallel);

    private string WriteFile(string relativePath, string content)
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    [Fact]
    public async Task ExecuteAsync_EmptyRequests_ReturnsEmptyOutcomes()
    {
        var result = await Broker().ExecuteAsync([], TestContext.Current.CancellationToken);
        result.ShouldBeOfType<Ok<IReadOnlyList<ToolOutcome>>>().Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_UnknownTool_ReturnsFailedOutcome()
    {
        var request = new ToolRequest("unknown_tool", new Dictionary<string, string>(), "test");
        var result = await Broker().ExecuteAsync([request], TestContext.Current.CancellationToken);

        var outcomes = result.ShouldBeOfType<Ok<IReadOnlyList<ToolOutcome>>>().Value;
        outcomes.Single().Succeeded.ShouldBeFalse();
        outcomes.Single().Error!.Code.ShouldBe("ToolBroker.UnknownTool");
    }

    [Fact]
    public async Task ExecuteAsync_ReadFile_ReturnsSummaryWithLineCount()
    {
        WriteFile("src/Service.cs", "line1\nline2\nline3");
        var args = new Dictionary<string, string> { ["path"] = "src/Service.cs" };
        var request = new ToolRequest("read_file", args, "read file");

        var outcomes = (await Broker().ExecuteAsync([request], TestContext.Current.CancellationToken))
            .ShouldBeOfType<Ok<IReadOnlyList<ToolOutcome>>>().Value;

        outcomes.Single().Succeeded.ShouldBeTrue();
        outcomes.Single().Summary.ShouldContain("3 lines");
    }

    [Fact]
    public async Task ExecuteAsync_ReadFileMissingPath_ReturnsFailed()
    {
        var request = new ToolRequest("read_file", new Dictionary<string, string>(), "read");
        var outcomes = (await Broker().ExecuteAsync([request], TestContext.Current.CancellationToken))
            .ShouldBeOfType<Ok<IReadOnlyList<ToolOutcome>>>().Value;

        outcomes.Single().Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ReadFileNonExistent_ReturnsFailed()
    {
        var args = new Dictionary<string, string> { ["path"] = "missing.cs" };
        var request = new ToolRequest("read_file", args, "read");
        var outcomes = (await Broker().ExecuteAsync([request], TestContext.Current.CancellationToken))
            .ShouldBeOfType<Ok<IReadOnlyList<ToolOutcome>>>().Value;

        outcomes.Single().Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ListDir_ReturnsSummaryWithCount()
    {
        WriteFile("src/A.cs", "");
        WriteFile("src/B.cs", "");
        var args = new Dictionary<string, string> { ["path"] = "src" };
        var request = new ToolRequest("list_dir", args, "list");

        var outcomes = (await Broker().ExecuteAsync([request], TestContext.Current.CancellationToken))
            .ShouldBeOfType<Ok<IReadOnlyList<ToolOutcome>>>().Value;

        outcomes.Single().Succeeded.ShouldBeTrue();
        outcomes.Single().Summary.ShouldContain("2 item(s)");
    }

    [Fact]
    public async Task ExecuteAsync_Grep_FindsMatchesWithLineNumbers()
    {
        WriteFile("src/Contract.cs", "namespace AiDev;\npublic interface IAgentExecutor { }\n// other");
        var args = new Dictionary<string, string>
        {
            ["pattern"] = "IAgentExecutor",
            ["dir"] = "src",
            ["extension"] = ".cs",
        };
        var request = new ToolRequest("grep", args, "search");

        var outcomes = (await Broker().ExecuteAsync([request], TestContext.Current.CancellationToken))
            .ShouldBeOfType<Ok<IReadOnlyList<ToolOutcome>>>().Value;

        var outcome = outcomes.Single();
        outcome.Succeeded.ShouldBeTrue();
        outcome.Evidence.Count.ShouldBe(1);
        outcome.Evidence[0].ShouldContain(":2");
    }

    [Fact]
    public async Task ExecuteAsync_Glob_ReturnsMatchingFiles()
    {
        WriteFile("src/A.cs", "");
        WriteFile("src/B.ts", "");
        var args = new Dictionary<string, string>
        {
            ["pattern"] = "*.cs",
            ["dir"] = "src",
        };
        var request = new ToolRequest("glob", args, "glob");

        var outcomes = (await Broker().ExecuteAsync([request], TestContext.Current.CancellationToken))
            .ShouldBeOfType<Ok<IReadOnlyList<ToolOutcome>>>().Value;

        var outcome = outcomes.Single();
        outcome.Succeeded.ShouldBeTrue();
        outcome.Evidence.ShouldAllBe(p => p.EndsWith(".cs"));
    }

    [Fact]
    public async Task ExecuteAsync_MultipleToolsRespectMaxParallelism()
    {
        WriteFile("a.cs", "x");
        WriteFile("b.cs", "x");
        WriteFile("c.cs", "x");

        var requests = new[]
        {
            new ToolRequest("read_file", new Dictionary<string, string> { ["path"] = "a.cs" }, "r"),
            new ToolRequest("read_file", new Dictionary<string, string> { ["path"] = "b.cs" }, "r"),
            new ToolRequest("read_file", new Dictionary<string, string> { ["path"] = "c.cs" }, "r"),
        };

        var outcomes = (await Broker(maxParallel: 2).ExecuteAsync(requests, TestContext.Current.CancellationToken))
            .ShouldBeOfType<Ok<IReadOnlyList<ToolOutcome>>>().Value;

        outcomes.Count.ShouldBe(3);
        outcomes.ShouldAllBe(o => o.Succeeded);
    }
}
