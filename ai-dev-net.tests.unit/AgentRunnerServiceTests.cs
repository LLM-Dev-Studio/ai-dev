using AiDev.Features.KnowledgeBase;
using AiDev.Features.Playbook;
using AiDev.Features.Secrets;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using System.Threading.Channels;

namespace AiDevNet.Tests.Unit;

public class AgentRunnerServiceTests
{
    [Fact]
    public async Task LaunchAgent_WhenAnthropicAgentUsesConfiguredAlias_ResolvesCanonicalModelId()
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var paths = new WorkspacePaths(root);
        var projectSlug = new ProjectSlug("demo-project");
        var agentSlug = new AgentSlug("pm-standard");
        var agentDir = paths.AgentDir(projectSlug, agentSlug).Value;
        var fileWriter = new AtomicFileWriter();
        var runner = CreateRunner(paths, fileWriter, out var executor);

        Directory.CreateDirectory(agentDir);
        Directory.CreateDirectory(paths.AgentInboxDir(projectSlug, agentSlug).Value);
        File.WriteAllText(paths.AgentClaudeMdPath(projectSlug, agentSlug).Value, "# PM Standard\n\nYou are PM Standard.\n");
        File.WriteAllText(paths.AgentJsonPath(projectSlug, agentSlug).Value, """
        {
          "slug": "pm-standard",
          "name": "PM Standard",
          "role": "PM",
          "description": "Routes work",
          "model": "sonnet",
          "executor": "anthropic",
          "status": "idle"
        }
        """);

        runner.LaunchAgent(projectSlug, agentSlug).ShouldBeTrue();

        var capturedModelId = await executor.CapturedModelId.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await WaitForAgentToFinishAsync(runner, projectSlug, agentSlug);

        capturedModelId.ShouldBe("claude-sonnet-4-6");
    }

    private static AgentRunnerService CreateRunner(WorkspacePaths paths, AtomicFileWriter fileWriter, out CapturingExecutor executor)
    {
        executor = new CapturingExecutor();

        var settings = new StudioSettingsService(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Models:sonnet"] = "claude-sonnet-4-6",
            })
            .Build());

        var modelRegistry = Substitute.For<IModelRegistry>();
        modelRegistry.Find(Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<string>(1) == "claude-sonnet-4-6"
                ? new ModelDescriptor("claude-sonnet-4-6", "Claude Sonnet 4.6", AgentExecutorName.AnthropicValue)
                : null);

        return new AgentRunnerService(
            paths,
            settings,
            [executor],
            modelRegistry,
            new MessageChangedNotifier(),
            new KbService(paths, fileWriter, new ProjectMutationCoordinator()),
            new PlaybookService(paths, fileWriter, new ProjectMutationCoordinator()),
            new SecretsService(paths, fileWriter),
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

    private sealed class CapturingExecutor : IAgentExecutor
    {
        private readonly TaskCompletionSource<string> _capturedModelId = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name => AgentExecutorName.AnthropicValue;
        public string DisplayName => "Anthropic API";
        public IReadOnlyList<ExecutorSkill> AvailableSkills => [];
        public IReadOnlyList<ModelDescriptor> KnownModels =>
        [
            new ModelDescriptor("claude-sonnet-4-6", "Claude Sonnet 4.6", AgentExecutorName.AnthropicValue),
        ];

        public Task<string> CapturedModelId => _capturedModelId.Task;

        public Task<ExecutorHealthResult> CheckHealthAsync(CancellationToken ct = default)
            => Task.FromResult(new ExecutorHealthResult(true, "Connected"));

        public Task<ExecutorResult> RunAsync(ExecutorContext context, ChannelWriter<string> output)
        {
            _capturedModelId.TrySetResult(context.ModelId);
            return Task.FromResult(new ExecutorResult(0));
        }
    }
}
