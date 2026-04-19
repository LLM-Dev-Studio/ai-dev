using AiDev;
using AiDev.Features.Agent;
using AiDev.Features.Decision;
using AiDev.Features.Workspace;
using AiDev.Models;
using AiDev.Models.Types;
using AiDev.Services;
using AiDev.WinUI.ViewModels;

using Microsoft.Extensions.Logging.Abstractions;

using System.Reflection;
using System.Text.Json;

namespace AiDevNet.Tests.WinUI;

public sealed class DecisionsViewModelTests : IDisposable
{
    private readonly string _rootPath;
    private readonly WorkspacePaths _paths;
    private readonly AtomicFileWriter _fileWriter = new();
    private readonly ProjectMutationCoordinator _coordinator = new();
    private readonly ProjectStateChangedNotifier _projectStateNotifier = new();

    public DecisionsViewModelTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), $"winui-vm-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootPath);
        _paths = new WorkspacePaths(new RootDir(_rootPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    [Fact]
    public async Task SelectDecisionAsync_WhenChatHistoryExists_LoadsChatMessages()
    {
        var (viewModel, _, _) = CreateViewModel();
        var projectSlug = new ProjectSlug("demo-project");

        var decision = new DecisionItem(
            filename: "decision-1.md",
            id: "decision-1",
            from: "pm-standard",
            subject: "Need direction",
            body: "Please choose.",
            date: DateTime.UtcNow,
            status: DecisionStatus.Pending);

        Directory.CreateDirectory(_paths.DecisionChatsDir(projectSlug).Value);
        var chatPath = Path.Combine(_paths.DecisionChatsDir(projectSlug).Value, "decision-1.jsonl");
        var message = new DecisionChatMessage("m1", "decision-1", "pm-standard", false, "Use Option A", DateTime.UtcNow);
        File.WriteAllText(chatPath, JsonSerializer.Serialize(message, JsonDefaults.Write) + Environment.NewLine);

        await viewModel.SelectDecisionAsync(decision);

        viewModel.SelectedDecision.ShouldNotBeNull();
        viewModel.SelectedDecision.Id.ShouldBe("decision-1");
        viewModel.ChatMessages.Count.ShouldBe(1);
        viewModel.ChatMessages[0].Content.ShouldBe("Use Option A");
    }

    [Fact]
    public async Task PollSelectedDecisionAsync_WhenOutboxContainsMatchingReply_AppendsReplyToChat()
    {
        var (viewModel, decisionsService, _) = CreateViewModel();
        var projectSlug = new ProjectSlug("demo-project");

        var createResult = decisionsService.CreateDecision(projectSlug, "pm-standard", "Need direction", Priority.Normal.Value, null, "Please choose.");
        createResult.ShouldBeOfType<Ok<Unit>>();

        var decisionId = Path.GetFileNameWithoutExtension(Directory.GetFiles(_paths.DecisionsPendingDir(projectSlug), "*.md").Single())!;
        var decision = decisionsService.GetDecision(projectSlug, decisionId);
        decision.ShouldNotBeNull();

        await viewModel.SelectDecisionAsync(decision!);

        var outboxDir = _paths.AgentOutboxDir(projectSlug, new AgentSlug("pm-standard")).Value;
        Directory.CreateDirectory(outboxDir);
        var outboxBody = FrontmatterParser.Stringify(
            new Dictionary<string, string>
            {
                ["type"] = "decision-reply",
                ["decision-id"] = decisionId,
                ["from"] = "pm-standard",
                ["date"] = DateTime.UtcNow.ToString("o"),
            },
            "I recommend Option B.");
        File.WriteAllText(Path.Combine(outboxDir, "reply.md"), outboxBody);

        await InvokePollSelectedDecisionAsync(viewModel);

        viewModel.ChatMessages.Count.ShouldBe(1);
        viewModel.ChatMessages[0].IsHuman.ShouldBeFalse();
        viewModel.ChatMessages[0].Content.ShouldBe("I recommend Option B.");
    }

    [Fact]
    public async Task PollSelectedDecisionAsync_WhenDecisionWasResolved_UpdatesSelectionAndBadges()
    {
        var (viewModel, decisionsService, mainViewModel) = CreateViewModel();
        var projectSlug = new ProjectSlug("demo-project");

        var createResult = decisionsService.CreateDecision(projectSlug, "pm-standard", "Need direction", Priority.Normal.Value, null, "Please choose.");
        createResult.ShouldBeOfType<Ok<Unit>>();

        var decisionId = Path.GetFileNameWithoutExtension(Directory.GetFiles(_paths.DecisionsPendingDir(projectSlug), "*.md").Single())!;
        var decision = decisionsService.GetDecision(projectSlug, decisionId);
        decision.ShouldNotBeNull();

        await viewModel.SelectDecisionAsync(decision!);
        mainViewModel.RefreshNavBadges();
        mainViewModel.PendingDecisionCount.ShouldBe(1);

        var resolveResult = await decisionsService.ResolveDecisionAsync(projectSlug, decisionId, "Resolved via human reply", TestContext.Current.CancellationToken);
        resolveResult.ShouldBeOfType<Ok<Unit>>();

        await InvokePollSelectedDecisionAsync(viewModel);

        viewModel.SelectedDecision.ShouldNotBeNull();
        viewModel.SelectedDecision.Status.IsResolved.ShouldBeTrue();
        mainViewModel.PendingDecisionCount.ShouldBe(0);
    }

    private (DecisionsViewModel ViewModel, DecisionsService DecisionsService, MainViewModel MainViewModel) CreateViewModel()
    {
        var projectSlug = new ProjectSlug("demo-project");
        var messagesService = new MessagesService(_paths);
        var dispatcher = Substitute.For<IDomainEventDispatcher>();
        dispatcher.Dispatch(Arg.Any<IReadOnlyList<DomainEvent>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<Unit>>(new Ok<Unit>(Unit.Value)));

        var decisionsService = new DecisionsService(_paths, dispatcher, _fileWriter, _coordinator, NullLogger<DecisionsService>.Instance);
        var healthMonitor = new ExecutorHealthMonitor([], NullLogger<ExecutorHealthMonitor>.Instance);
        var mainViewModel = new MainViewModel(healthMonitor, messagesService, decisionsService)
        {
            ActiveProject = new ProjectDetail
            {
                Slug = projectSlug,
                Name = "Demo Project",
                Description = "Test project",
            }
        };

        var runner = Substitute.For<IAgentRunnerService>();
        var chatService = new DecisionChatService(_paths, runner, _projectStateNotifier, NullLogger<DecisionChatService>.Instance);
        var uiDispatcher = new ImmediateDispatcher();

        var viewModel = new DecisionsViewModel(decisionsService, chatService, mainViewModel, uiDispatcher);
        return (viewModel, decisionsService, mainViewModel);
    }

    private static async Task InvokePollSelectedDecisionAsync(DecisionsViewModel viewModel)
    {
        var method = typeof(DecisionsViewModel).GetMethod("PollSelectedDecisionAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        method.ShouldNotBeNull();
        var task = method!.Invoke(viewModel, null) as Task;
        task.ShouldNotBeNull();
        var nonNullTask = task;
        await nonNullTask!;
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Enqueue(Action action) => action();
    }
}
