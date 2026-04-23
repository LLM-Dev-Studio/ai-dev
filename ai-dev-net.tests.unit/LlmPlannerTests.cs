using AiDev.Core.Local.Contracts;
using AiDev.Core.Local.Implementation;

namespace AiDevNet.Tests.Unit;

public class LlmPlannerTests
{
    private static readonly RuntimeModelProfile OllamaProfile = new(
        "llama3.2", "large-local", "ollama", 32_000, false);

    private static readonly LocalRuntimeState AnyState = new(
        Objective: new LocalObjective("Find all IAgentExecutor implementations", null, null, Guid.NewGuid()),
        Transcript: new RuntimeTranscript([], []),
        Budget: new RuntimeBudget(MaxToolCalls: 50, MaxExpandedFiles: 10, MaxRetriesPerError: 2, MaxContextTokens: 32_000),
        ModelProfile: OllamaProfile,
        StartedAtUtc: DateTimeOffset.UtcNow,
        Iteration: 0);

    private static ILlmClient ClientReturning(string response)
    {
        var mock = Substitute.For<ILlmClient>();
        mock.Provider.Returns("ollama");
        mock.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Ok<string>(response));
        return mock;
    }

    [Fact]
    public async Task PlanNextAsync_WhenClientReturnsValidJson_ReturnsParsedPlan()
    {
        const string json = """
            {
              "intent": "List executor files",
              "toolRequests": [
                { "toolName": "glob", "arguments": { "pattern": "*.cs", "dir": "ai-dev.executor.ollama" }, "reason": "find files" }
              ],
              "expectedOutcome": "List of executor cs files",
              "requiresUserInput": false
            }
            """;

        var result = await new LlmPlanner([ClientReturning(json)])
            .PlanNextAsync(AnyState, TestContext.Current.CancellationToken);

        var plan = result.ShouldBeOfType<Ok<RuntimeActionPlan>>().Value;
        plan.Intent.ShouldBe("List executor files");
        plan.ToolRequests.Count.ShouldBe(1);
        plan.ToolRequests[0].ToolName.ShouldBe("glob");
        plan.RequiresUserInput.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanNextAsync_WhenClientReturnsEmptyToolRequests_ReturnsDonePlan()
    {
        const string json = """
            {
              "intent": "Objective complete",
              "toolRequests": [],
              "expectedOutcome": "done",
              "requiresUserInput": false
            }
            """;

        var result = await new LlmPlanner([ClientReturning(json)])
            .PlanNextAsync(AnyState, TestContext.Current.CancellationToken);

        var plan = result.ShouldBeOfType<Ok<RuntimeActionPlan>>().Value;
        plan.ToolRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task PlanNextAsync_WhenClientReturnsJsonWithPreamble_ExtractsJsonCorrectly()
    {
        const string response = """
            Sure! Here is the plan:
            {
              "intent": "Read file",
              "toolRequests": [],
              "expectedOutcome": "content",
              "requiresUserInput": false
            }
            Hope this helps!
            """;

        var result = await new LlmPlanner([ClientReturning(response)])
            .PlanNextAsync(AnyState, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Ok<RuntimeActionPlan>>().Value.Intent.ShouldBe("Read file");
    }

    [Fact]
    public async Task PlanNextAsync_WhenClientReturnsInvalidJson_RetriesAndReturnsErr()
    {
        var mock = Substitute.For<ILlmClient>();
        mock.Provider.Returns("ollama");
        mock.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Ok<string>("Not valid JSON at all"));

        var result = await new LlmPlanner([mock])
            .PlanNextAsync(AnyState, TestContext.Current.CancellationToken);

        var err = result.ShouldBeOfType<Err<RuntimeActionPlan>>();
        err.Error.Code.ShouldBe("LlmPlanner.ParseFailed");

        // AnyState.Budget.MaxRetriesPerError = 2 → 3 attempts total
        await mock.Received(3).CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlanNextAsync_WhenNoClientForProvider_ReturnsNoClientErr()
    {
        var wrongProvider = Substitute.For<ILlmClient>();
        wrongProvider.Provider.Returns("lmstudio");

        var result = await new LlmPlanner([wrongProvider])
            .PlanNextAsync(AnyState, TestContext.Current.CancellationToken);

        var err = result.ShouldBeOfType<Err<RuntimeActionPlan>>();
        err.Error.Code.ShouldBe("LlmPlanner.NoClient");
    }

    [Fact]
    public async Task PlanNextAsync_WhenClientReturnsRequiresUserInput_ReturnsTrueFlag()
    {
        const string json = """
            {
              "intent": "Need clarification",
              "toolRequests": [],
              "expectedOutcome": "user decision",
              "requiresUserInput": true
            }
            """;

        var result = await new LlmPlanner([ClientReturning(json)])
            .PlanNextAsync(AnyState, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Ok<RuntimeActionPlan>>().Value.RequiresUserInput.ShouldBeTrue();
    }
}
