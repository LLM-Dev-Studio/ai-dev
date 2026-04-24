using AiDev.Core.Local.Contracts;
using AiDev.Core.Local.Implementation;

namespace AiDevNet.Tests.Unit;

public class RoleBasedLlmPlannerTests
{
    private static readonly RuntimeModelProfile OllamaProfile =
        new("llama3.2", "large-local", "ollama", 32_000, false);

    private static LocalRuntimeState StateAt(int iteration, IReadOnlyList<RuntimeObservation>? obs = null)
        => new(
            Objective: new LocalObjective("Find IAgentExecutor", null, null, Guid.NewGuid()),
            Transcript: new RuntimeTranscript(obs ?? [], []),
            Budget: new RuntimeBudget(50, 10, 2, 32_000),
            ModelProfile: OllamaProfile,
            StartedAtUtc: DateTimeOffset.UtcNow,
            Iteration: iteration);

    private static RuntimeObservation Obs(string source, string summary)
        => new(DateTimeOffset.UtcNow, source, summary, []);

    // ── Role selection ──────────────────────────────────────────────────────

    [Fact]
    public void SelectRole_FirstIteration_ReturnsPlanner()
        => RoleBasedLlmPlanner.SelectRole(StateAt(0)).ShouldBe(LocalAgentRole.Planner);

    [Fact]
    public void SelectRole_NoObservations_ReturnsPlanner()
        => RoleBasedLlmPlanner.SelectRole(StateAt(2, [])).ShouldBe(LocalAgentRole.Planner);

    [Fact]
    public void SelectRole_AfterGlobOnly_ReturnsResearcher()
    {
        var state = StateAt(1, [Obs("glob", "Found 3 files"), Obs("list_dir", "2 dirs")]);
        RoleBasedLlmPlanner.SelectRole(state).ShouldBe(LocalAgentRole.Researcher);
    }

    [Fact]
    public void SelectRole_AfterReadFile_ReturnsCoder()
    {
        var state = StateAt(2, [Obs("glob", "Found files"), Obs("read_file", "Read 50 lines")]);
        RoleBasedLlmPlanner.SelectRole(state).ShouldBe(LocalAgentRole.Coder);
    }

    [Fact]
    public void SelectRole_AfterGrep_ReturnsCoder()
    {
        var state = StateAt(1, [Obs("grep", "Found 3 matches")]);
        RoleBasedLlmPlanner.SelectRole(state).ShouldBe(LocalAgentRole.Coder);
    }

    // ── Tool whitelist filtering ─────────────────────────────────────────────

    [Fact]
    public async Task PlanNextAsync_PlannerRole_FiltersOutDisallowedTools()
    {
        // LLM tries to use read_file, which is not in the Planner whitelist
        const string json = """
            {
              "intent": "map structure",
              "toolRequests": [
                { "toolName": "glob",      "arguments": { "pattern": "*.cs" }, "reason": "find files" },
                { "toolName": "read_file", "arguments": { "path": "foo.cs"  }, "reason": "read"       }
              ],
              "expectedOutcome": "structure",
              "requiresUserInput": false
            }
            """;

        var client = MockClient("ollama", json);
        var planner = new RoleBasedLlmPlanner([client]);

        // iteration=0 → Planner role
        var result = await planner.PlanNextAsync(StateAt(0), TestContext.Current.CancellationToken);

        var plan = result.ShouldBeOfType<Ok<RuntimeActionPlan>>().Value;
        plan.ToolRequests.Count.ShouldBe(1);
        plan.ToolRequests[0].ToolName.ShouldBe("glob");
    }

    [Fact]
    public async Task PlanNextAsync_ResearcherRole_AllowsReadFileAndGrep()
    {
        const string json = """
            {
              "intent": "read executor",
              "toolRequests": [
                { "toolName": "read_file", "arguments": { "path": "x.cs" }, "reason": "read" },
                { "toolName": "grep",      "arguments": { "pattern": "IAgentExecutor" }, "reason": "search" }
              ],
              "expectedOutcome": "details",
              "requiresUserInput": false
            }
            """;

        var client = MockClient("ollama", json);
        var planner = new RoleBasedLlmPlanner([client]);

        // iteration=1, only glob obs → Researcher
        var state = StateAt(1, [Obs("glob", "3 files")]);
        var result = await planner.PlanNextAsync(state, TestContext.Current.CancellationToken);

        var plan = result.ShouldBeOfType<Ok<RuntimeActionPlan>>().Value;
        plan.ToolRequests.Count.ShouldBe(2);
    }

    // ── Parse and retry ──────────────────────────────────────────────────────

    [Fact]
    public async Task PlanNextAsync_NoMatchingClient_ReturnsNoClientErr()
    {
        var client = MockClient("lmstudio", "{}");
        var result = await new RoleBasedLlmPlanner([client])
            .PlanNextAsync(StateAt(0), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Err<RuntimeActionPlan>>().Error.Code.ShouldBe("LlmPlanner.NoClient");
    }

    [Fact]
    public async Task PlanNextAsync_InvalidJson_RetriesAndFails()
    {
        var client = Substitute.For<ILlmClient>();
        client.Provider.Returns("ollama");
        client.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Ok<string>("not json"));

        var result = await new RoleBasedLlmPlanner([client])
            .PlanNextAsync(StateAt(0), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Err<RuntimeActionPlan>>().Error.Code.ShouldBe("LlmPlanner.ParseFailed");
        // Budget.MaxRetriesPerError=2 → 3 attempts
        await client.Received(3).CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static ILlmClient MockClient(string provider, string response)
    {
        var c = Substitute.For<ILlmClient>();
        c.Provider.Returns(provider);
        c.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Ok<string>(response));
        return c;
    }
}
