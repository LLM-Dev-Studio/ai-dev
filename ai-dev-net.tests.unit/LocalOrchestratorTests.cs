using AiDev.Core.Local.Contracts;
using AiDev.Core.Local.Orchestration;

namespace AiDevNet.Tests.Unit;

public class LocalOrchestratorTests
{
    private static readonly RuntimeModelProfile AnyProfile = new(
        ModelId: "test-model",
        ModelClass: "large-local",
        Provider: "ollama",
        MaxInputTokens: 32_000,
        SupportsParallelTools: false);

    private static readonly RuntimeModelStrategy DefaultStrategy = new(
        PlanningDepth: 1,
        DiscoveryBreadth: 3,
        MaxParallelTools: 1,
        MinimumConfidenceToProceed: 0.5m,
        CompactionInterval: 5);

    private static readonly LocalObjective AnyObjective = new(
        Goal: "Test goal",
        SuccessCriteria: null,
        CodebaseRoot: null,
        CorrelationId: Guid.NewGuid());

    private static readonly CompactionSnapshot EmptySnapshot = new(
        CompactSummary: string.Empty,
        Facts: [],
        OpenQuestions: [],
        EstimatedTokens: 0);

    private static LocalOrchestrator Create(
        ILocalPlanner planner,
        ILocalToolBroker? toolBroker = null,
        IContextCompactor? compactor = null,
        IRuntimeMemoryStore? memoryStore = null,
        IModelStrategyResolver? resolver = null,
        LocalOrchestratorOptions? options = null)
    {
        if (toolBroker is null)
            toolBroker = Substitute.For<ILocalToolBroker>();

        if (compactor is null)
        {
            compactor = Substitute.For<IContextCompactor>();
            compactor.Compact(Arg.Any<LocalRuntimeState>()).Returns(new Ok<CompactionSnapshot>(EmptySnapshot));
        }

        if (memoryStore is null)
        {
            memoryStore = Substitute.For<IRuntimeMemoryStore>();
            memoryStore.SaveSnapshotAsync(Arg.Any<Guid>(), Arg.Any<CompactionSnapshot>(), Arg.Any<CancellationToken>())
                .Returns(new Ok<AiDev.Models.Unit>(default));
        }

        if (resolver is null)
        {
            resolver = Substitute.For<IModelStrategyResolver>();
            resolver.Resolve(Arg.Any<RuntimeModelProfile>()).Returns(new Ok<RuntimeModelStrategy>(DefaultStrategy));
        }

        return new LocalOrchestrator(planner, toolBroker, compactor, memoryStore, resolver,
            options ?? LocalOrchestratorOptions.Default);
    }

    [Fact]
    public async Task RunAsync_WhenPlannerReturnsEmptyPlan_TerminatesWithSuccess()
    {
        var planner = Substitute.For<ILocalPlanner>();
        planner.PlanNextAsync(Arg.Any<LocalRuntimeState>(), Arg.Any<CancellationToken>())
            .Returns(new Ok<RuntimeActionPlan>(new RuntimeActionPlan(
                Intent: "done",
                ToolRequests: [],
                ExpectedOutcome: "done",
                RequiresUserInput: false)));

        var orchestrator = Create(planner);

        var result = await orchestrator.RunAsync(AnyObjective, AnyProfile, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Ok<CompactionSnapshot>>();
    }

    [Fact]
    public async Task RunAsync_WhenPlannerRequiresUserInput_ReturnsBlockedError()
    {
        var planner = Substitute.For<ILocalPlanner>();
        planner.PlanNextAsync(Arg.Any<LocalRuntimeState>(), Arg.Any<CancellationToken>())
            .Returns(new Ok<RuntimeActionPlan>(new RuntimeActionPlan(
                Intent: "need user input",
                ToolRequests: [],
                ExpectedOutcome: string.Empty,
                RequiresUserInput: true)));

        var orchestrator = Create(planner);

        var result = await orchestrator.RunAsync(AnyObjective, AnyProfile, TestContext.Current.CancellationToken);

        var err = result.ShouldBeOfType<Err<CompactionSnapshot>>();
        err.Error.Code.ShouldBe("LocalOrchestrator.BlockedOnInput");
    }

    [Fact]
    public async Task RunAsync_WhenPlannerReturnsError_PropagatesError()
    {
        var plannerError = new DomainError("Planner.Failed", "Planner failed.");
        var planner = Substitute.For<ILocalPlanner>();
        planner.PlanNextAsync(Arg.Any<LocalRuntimeState>(), Arg.Any<CancellationToken>())
            .Returns(new Err<RuntimeActionPlan>(plannerError));

        var orchestrator = Create(planner);

        var result = await orchestrator.RunAsync(AnyObjective, AnyProfile, TestContext.Current.CancellationToken);

        var err = result.ShouldBeOfType<Err<CompactionSnapshot>>();
        err.Error.ShouldBe(plannerError);
    }

    [Fact]
    public async Task RunAsync_WhenToolCallBudgetExhausted_ReturnsBudgetError()
    {
        var tightBudgetOptions = new LocalOrchestratorOptions(
            MaxIterations: 10,
            DefaultBudget: new RuntimeBudget(
                MaxToolCalls: 1,
                MaxExpandedFiles: 10,
                MaxRetriesPerError: 3,
                MaxContextTokens: 32_000));

        var planner = Substitute.For<ILocalPlanner>();
        planner.PlanNextAsync(Arg.Any<LocalRuntimeState>(), Arg.Any<CancellationToken>())
            .Returns(new Ok<RuntimeActionPlan>(new RuntimeActionPlan(
                Intent: "run tool",
                ToolRequests: [new ToolRequest("read", new Dictionary<string, string>(), "read file")],
                ExpectedOutcome: "file contents",
                RequiresUserInput: false)));

        var toolBroker = Substitute.For<ILocalToolBroker>();
        toolBroker.ExecuteAsync(Arg.Any<IReadOnlyList<ToolRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new Ok<IReadOnlyList<ToolOutcome>>([
                new ToolOutcome("read", true, "contents", []),
            ]));

        var orchestrator = Create(planner, toolBroker: toolBroker, options: tightBudgetOptions);

        var result = await orchestrator.RunAsync(AnyObjective, AnyProfile, TestContext.Current.CancellationToken);

        var err = result.ShouldBeOfType<Err<CompactionSnapshot>>();
        err.Error.Code.ShouldBe("LocalOrchestrator.BudgetExhausted");
    }

    [Fact]
    public async Task RunAsync_WhenStrategyResolverFails_ReturnsStrategyError()
    {
        var strategyError = new DomainError("Strategy.Failed", "Cannot resolve strategy.");
        var resolver = Substitute.For<IModelStrategyResolver>();
        resolver.Resolve(Arg.Any<RuntimeModelProfile>()).Returns(new Err<RuntimeModelStrategy>(strategyError));

        var planner = Substitute.For<ILocalPlanner>();
        var orchestrator = Create(planner, resolver: resolver);

        var result = await orchestrator.RunAsync(AnyObjective, AnyProfile, TestContext.Current.CancellationToken);

        var err = result.ShouldBeOfType<Err<CompactionSnapshot>>();
        err.Error.ShouldBe(strategyError);
    }

    [Fact]
    public async Task RunAsync_StrategyToolCallBudgetOverridesDefaultBudget()
    {
        // DefaultBudget allows 50 tool calls but the resolved strategy caps at 1.
        var resolver = Substitute.For<IModelStrategyResolver>();
        resolver.Resolve(Arg.Any<RuntimeModelProfile>()).Returns(new Ok<RuntimeModelStrategy>(
            DefaultStrategy with { ToolCallBudget = 1 }));

        var planner = Substitute.For<ILocalPlanner>();
        planner.PlanNextAsync(Arg.Any<LocalRuntimeState>(), Arg.Any<CancellationToken>())
            .Returns(new Ok<RuntimeActionPlan>(new RuntimeActionPlan(
                Intent: "run tool",
                ToolRequests: [new ToolRequest("read", new Dictionary<string, string>(), "read file")],
                ExpectedOutcome: "file contents",
                RequiresUserInput: false)));

        var toolBroker = Substitute.For<ILocalToolBroker>();
        toolBroker.ExecuteAsync(Arg.Any<IReadOnlyList<ToolRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new Ok<IReadOnlyList<ToolOutcome>>([new ToolOutcome("read", true, "contents", [])]));

        var orchestrator = Create(planner, toolBroker: toolBroker, resolver: resolver);

        var result = await orchestrator.RunAsync(AnyObjective, AnyProfile, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Err<CompactionSnapshot>>().Error.Code.ShouldBe("LocalOrchestrator.BudgetExhausted");
    }
}
