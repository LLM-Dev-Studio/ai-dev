using AiDev.Features.Decision;
using AiDev.Features.Insights;
using AiDev.Features.KnowledgeBase;
using AiDev.Features.Playbook;
using AiDev.Features.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using System.Threading.Channels;

namespace AiDevNet.Tests.Unit;

public class DecisionChatServiceTests
{
    [Fact]
    public void GetMessages_WhenChatFileContainsIndentedJsonObjects_ParsesAllMessages()
    {
        var paths = CreatePaths();
        var service = new DecisionChatService(paths, null!, new DecisionChangedNotifier(), NullLogger<DecisionChatService>.Instance);
        var projectSlug = new ProjectSlug("demo-project");
        var decisionId = "20260410-093000-offline-executor-selection";
        var chatDir = paths.DecisionChatsDir(projectSlug).Value;
        Directory.CreateDirectory(chatDir);

        var first = new DecisionChatMessage("1", decisionId, "human", true, "soft-warn", DateTime.UtcNow);
        var second = new DecisionChatMessage("2", decisionId, "human", true, "Option A", DateTime.UtcNow.AddMinutes(1));
        var chatPath = Path.Combine(chatDir, $"{decisionId}.jsonl");
        File.WriteAllText(chatPath,
            JsonSerializer.Serialize(first, JsonDefaults.Write) + Environment.NewLine +
            JsonSerializer.Serialize(second, JsonDefaults.Write) + Environment.NewLine);

        var messages = service.GetMessages(projectSlug, decisionId);

        messages.Count.ShouldBe(2);
        messages[0].Content.ShouldBe("soft-warn");
        messages[1].Content.ShouldBe("Option A");
    }

    [Fact]
    public async Task SendHumanMessage_WritesSingleLineJsonEntry()
    {
        var paths = CreatePaths();
        var fileWriter = new AtomicFileWriter();
        var runner = CreateRunner(paths, fileWriter);
        var service = new DecisionChatService(paths, runner, new DecisionChangedNotifier(), NullLogger<DecisionChatService>.Instance);
        var projectSlug = new ProjectSlug("demo-project");
        const string decisionId = "20260410-093000-offline-executor-selection";
        const string agentSlug = "pm-standard";
        var agent = new AgentSlug(agentSlug);
        var agentDir = paths.AgentDir(projectSlug, agent).Value;
        Directory.CreateDirectory(agentDir);
        File.WriteAllText(paths.AgentJsonPath(projectSlug, agent).Value, """
        {
          "slug": "pm-standard",
          "name": "PM Standard",
          "role": "PM",
          "description": "Routes work",
          "model": "claude-sonnet-4-6",
          "executor": "claude",
          "status": "idle"
        }
        """);
        File.WriteAllText(paths.AgentClaudeMdPath(projectSlug, agent).Value, "# PM Standard\n\nYou are PM Standard.\n");

        var error = service.SendHumanMessage(projectSlug, decisionId, agentSlug, "Option A");
        await WaitForAgentToFinishAsync(runner, projectSlug, agent);

        error.ShouldBeNull();
        var chatPath = Path.Combine(paths.DecisionChatsDir(projectSlug).Value, $"{decisionId}.jsonl");
        var lines = File.ReadAllLines(chatPath);
        lines.Length.ShouldBe(1);
        lines[0].ShouldContain("\"content\":\"Option A\"");
    }

    private static WorkspacePaths CreatePaths()
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        return new WorkspacePaths(root);
    }

    private static AgentRunnerService CreateRunner(WorkspacePaths paths, AtomicFileWriter fileWriter)
    {
        var settings = new StudioSettingsService(new ConfigurationBuilder().Build());
        var modelRegistry = Substitute.For<IModelRegistry>();
        modelRegistry.Find(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new ModelDescriptor("claude-sonnet-4-6", "Claude Sonnet 4.6", AgentExecutorName.ClaudeValue));

        return new AgentRunnerService(
            paths,
            settings,
            [new ImmediateExecutor()],
            modelRegistry,
            new MessageChangedNotifier(),
            new KbService(paths, fileWriter, new ProjectMutationCoordinator()),
            new PlaybookService(paths, fileWriter, new ProjectMutationCoordinator()),
            new SecretsService(paths, fileWriter),
            new InsightsService(Substitute.For<IHttpClientFactory>(), settings, NullLogger<InsightsService>.Instance),
            NullLogger<AgentRunnerService>.Instance);
    }

    private static async Task WaitForAgentToFinishAsync(AgentRunnerService runner, ProjectSlug projectSlug, AgentSlug agentSlug)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (!runner.IsRunning(projectSlug, agentSlug))
                return;

            await Task.Delay(50);
        }

        throw new TimeoutException("Agent runner did not finish within the expected time.");
    }

    private sealed class ImmediateExecutor : IAgentExecutor
    {
        public string Name => AgentExecutorName.ClaudeValue;
        public string DisplayName => "Claude CLI";
        public IReadOnlyList<ExecutorSkill> AvailableSkills => [];
        public IReadOnlyList<ModelDescriptor> KnownModels =>
        [
            new ModelDescriptor("claude-sonnet-4-6", "Claude Sonnet 4.6", AgentExecutorName.ClaudeValue),
        ];

        public Task<ExecutorHealthResult> CheckHealthAsync(CancellationToken ct = default)
            => Task.FromResult(new ExecutorHealthResult(true, "Connected"));

        public Task<ExecutorResult> RunAsync(ExecutorContext context, ChannelWriter<string> output)
            => Task.FromResult(new ExecutorResult(0));
    }
}
