using AiDev;
using AiDev.Core.Local.Contracts;
using AiDev.Core.Local.Implementation;
using AiDev.Core.Local.Orchestration;
using AiDev.Models;

namespace AiDevNet.Tests.Integration;

/// <summary>
/// End-to-end tests for the full orchestration loop using real implementations
/// except ILlmClient, which is faked so tests run without a live Ollama instance.
/// </summary>
public class LocalOrchestratorEndToEndTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public LocalOrchestratorEndToEndTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "src"));
        File.WriteAllText(
            Path.Combine(_root, "src", "Service.cs"),
            "namespace AiDev; public interface IAgentExecutor { Task RunAsync(); }");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static readonly RuntimeModelProfile OllamaProfile = new(
        "llama3.2", "large-local", "ollama", 32_000, false);

    private LocalOrchestrator BuildOrchestrator(ILlmClient llmClient, LocalOrchestratorOptions? options = null)
    {
        var paths = new WorkspacePaths(new RootDir(_root));
        var resolvedOptions = options ?? LocalOrchestratorOptions.Default;

        var planner = new LlmPlanner([llmClient]);
        var toolBroker = new LocalToolBroker(_root, 1);
        var compactor = new RuleBasedContextCompactor();
        var memoryStore = new InMemoryRuntimeMemoryStore();
        var resolver = new StaticModelStrategyResolver();

        return new LocalOrchestrator(planner, toolBroker, compactor, memoryStore, resolver, resolvedOptions);
    }

    private static ILlmClient FakeClient(Queue<string> responses)
    {
        var mock = Substitute.For<ILlmClient>();
        mock.Provider.Returns("ollama");
        mock.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Result<string>>(
                responses.Count > 0
                    ? new Ok<string>(responses.Dequeue())
                    : new Ok<string>(DonePlan())));
        return mock;
    }

    private static string DonePlan() => """
        {"intent":"done","toolRequests":[],"expectedOutcome":"objective complete","requiresUserInput":false}
        """;

    private static string GrepPlan(string pattern) => $$"""
        {
          "intent": "Search for {{pattern}}",
          "toolRequests": [{ "toolName": "grep", "arguments": { "pattern": "{{pattern}}", "dir": "src" }, "reason": "find it" }],
          "expectedOutcome": "match found",
          "requiresUserInput": false
        }
        """;

    [Fact]
    public async Task FullLoop_SingleToolCallThenDone_CompletesWithOkSnapshot()
    {
        var responses = new Queue<string>([GrepPlan("IAgentExecutor"), DonePlan()]);
        var orchestrator = BuildOrchestrator(FakeClient(responses));
        var objective = new LocalObjective(
            "Find all IAgentExecutor implementations",
            SuccessCriteria: null,
            CodebaseRoot: _root,
            CorrelationId: Guid.NewGuid());

        var result = await orchestrator.RunAsync(objective, OllamaProfile, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Ok<CompactionSnapshot>>();
    }

    [Fact]
    public async Task FullLoop_ContextTokensRemainsUnderBudget()
    {
        var responses = new Queue<string>([GrepPlan("IAgentExecutor"), DonePlan()]);
        var orchestrator = BuildOrchestrator(FakeClient(responses));
        var objective = new LocalObjective(
            "Find all IAgentExecutor implementations", null, _root, Guid.NewGuid());

        var result = await orchestrator.RunAsync(objective, OllamaProfile, TestContext.Current.CancellationToken);

        var snapshot = result.ShouldBeOfType<Ok<CompactionSnapshot>>().Value;
        snapshot.EstimatedTokens.ShouldBeLessThanOrEqualTo(LocalOrchestratorOptions.Default.DefaultBudget.MaxContextTokens);
    }

    [Fact]
    public async Task FullLoop_WhenPlannerAlwaysFails_SurfacesTypedDomainError()
    {
        var options = new LocalOrchestratorOptions(
            MaxIterations: 5,
            DefaultBudget: new RuntimeBudget(MaxToolCalls: 50, MaxExpandedFiles: 10, MaxRetriesPerError: 1, MaxContextTokens: 32_000));

        var mock = Substitute.For<ILlmClient>();
        mock.Provider.Returns("ollama");
        mock.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Ok<string>("not valid json"));

        var orchestrator = BuildOrchestrator(mock, options);
        var objective = new LocalObjective("Goal", null, _root, Guid.NewGuid());

        var result = await orchestrator.RunAsync(objective, OllamaProfile, TestContext.Current.CancellationToken);

        var err = result.ShouldBeOfType<Err<CompactionSnapshot>>();
        err.Error.Code.ShouldBe("LlmPlanner.ParseFailed");
    }

    [Fact]
    public async Task FullLoop_ToolCallBudgetExhausted_SurfacesBudgetError()
    {
        var tightOptions = new LocalOrchestratorOptions(
            MaxIterations: 20,
            DefaultBudget: new RuntimeBudget(MaxToolCalls: 1, MaxExpandedFiles: 10, MaxRetriesPerError: 3, MaxContextTokens: 32_000));

        // Always return a plan with a tool call — never terminates naturally
        var mock = Substitute.For<ILlmClient>();
        mock.Provider.Returns("ollama");
        mock.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Ok<string>(GrepPlan("IAgentExecutor")));

        var orchestrator = BuildOrchestrator(mock, tightOptions);
        var objective = new LocalObjective("Find everything", null, _root, Guid.NewGuid());

        var result = await orchestrator.RunAsync(objective, OllamaProfile, TestContext.Current.CancellationToken);

        var err = result.ShouldBeOfType<Err<CompactionSnapshot>>();
        err.Error.Code.ShouldBe("LocalOrchestrator.BudgetExhausted");
    }
}
