using AiDev;
using AiDev.Executors;
using AiDev.Features.Agent;
using AiDev.Features.Decision;
using AiDev.Features.Workspace;
using AiDev.Models;
using AiDev.Models.Types;
using AiDev.Services;
using AiDev.WinUI.ViewModels;

using Microsoft.Extensions.Logging.Abstractions;

using System.Threading.Channels;

namespace AiDevNet.Tests.WinUI;

public sealed class ProjectSettingsViewModelTests : IDisposable
{
    private readonly string _rootPath;
    private readonly WorkspacePaths _paths;
    private readonly AtomicFileWriter _fileWriter = new();
    private readonly ProjectMutationCoordinator _coordinator = new();

    public ProjectSettingsViewModelTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), $"project-settings-vm-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootPath);
        _paths = new WorkspacePaths(new RootDir(_rootPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    [Fact]
    public void OpenBulkSwitch_WhenNoHealthyExecutors_FallsBackToSupportedExecutors()
    {
        var (viewModel, _, _, _) = CreateViewModel();

        viewModel.OpenBulkSwitch();

        viewModel.ShowBulkSwitch.ShouldBeTrue();
        viewModel.AvailableExecutors.Count.ShouldBe(AgentExecutorName.Supported.Count);
        viewModel.AvailableExecutors.ShouldContain(AgentExecutorName.ClaudeValue);
        viewModel.AvailableExecutors.ShouldContain(AgentExecutorName.OllamaValue);
    }

    [Fact]
    public async Task ApplyBulkSwitchToExecutorAsync_WhenNoActiveProject_ReturnsError()
    {
        var (viewModel, _, _, _) = CreateViewModel(withActiveProject: false);

        var error = await viewModel.ApplyBulkSwitchToExecutorAsync(AgentExecutorName.AnthropicValue);

        error.ShouldBe("No active project is selected.");
        viewModel.BulkSwitchError.ShouldBe(error);
        viewModel.ApplyingBulkSwitch.ShouldBeFalse();
    }

    [Fact]
    public async Task ApplyBulkSwitchToExecutorAsync_WhenTargetExecutorHasNoModels_ReturnsError()
    {
        var (viewModel, _, agentService, _) = CreateViewModel();
        SeedProjectWithAgent(agentService);
        viewModel.Load();

        var error = await viewModel.ApplyBulkSwitchToExecutorAsync(AgentExecutorName.OllamaValue);

        error.ShouldBe("Ollama has no available models.");
        viewModel.BulkSwitchError.ShouldBe(error);
        viewModel.ApplyingBulkSwitch.ShouldBeFalse();
    }

    [Fact]
    public async Task ApplyBulkSwitchToExecutorAsync_WhenValid_UpdatesExecutorModelAndThinkingLevel()
    {
        var modelRegistry = Substitute.For<IModelRegistry>();
        modelRegistry.GetModelsForExecutor(AgentExecutorName.AnthropicValue).Returns([
            new ModelDescriptor("anthropic-haiku", "Anthropic Haiku", AgentExecutorName.AnthropicValue)
        ]);

        var (viewModel, _, agentService, _) = CreateViewModel(modelRegistry: modelRegistry);
        SeedProjectWithAgent(agentService);
        viewModel.Load();
        viewModel.OpenBulkSwitch();

        var error = await viewModel.ApplyBulkSwitchToExecutorAsync(AgentExecutorName.AnthropicValue);
        var updatedAgent = agentService.LoadAgent(new ProjectSlug("demo-project"), new AgentSlug("dev-alex"));

        error.ShouldBeNull();
        updatedAgent.ShouldNotBeNull();
        updatedAgent!.Executor.ShouldBe(AgentExecutorName.Anthropic);
        updatedAgent.Model.ShouldBe("anthropic-haiku");
        updatedAgent.ThinkingLevel.ShouldBe(ThinkingLevel.Off);
        viewModel.ShowBulkSwitch.ShouldBeFalse();
        viewModel.BulkTargetExecutor.ShouldBe(string.Empty);
        viewModel.ApplyingBulkSwitch.ShouldBeFalse();
    }

    [Fact]
    public async Task ApplyBulkSwitchToExecutorAsync_WhenModelOverrideProvided_UsesOverrideForAllAgents()
    {
        var modelRegistry = Substitute.For<IModelRegistry>();
        modelRegistry.GetModelsForExecutor(AgentExecutorName.AnthropicValue).Returns([
            new ModelDescriptor("anthropic-haiku", "Anthropic Haiku", AgentExecutorName.AnthropicValue),
            new ModelDescriptor("anthropic-sonnet", "Anthropic Sonnet", AgentExecutorName.AnthropicValue, ModelCapabilities.Streaming | ModelCapabilities.ToolCalling | ModelCapabilities.Reasoning)
        ]);

        var (viewModel, _, agentService, _) = CreateViewModel(modelRegistry: modelRegistry);
        SeedProjectWithAgent(agentService);
        viewModel.Load();

        var error = await viewModel.ApplyBulkSwitchToExecutorAsync(AgentExecutorName.AnthropicValue, "anthropic-sonnet");
        var updatedAgent = agentService.LoadAgent(new ProjectSlug("demo-project"), new AgentSlug("dev-alex"));

        error.ShouldBeNull();
        updatedAgent.ShouldNotBeNull();
        updatedAgent!.Executor.ShouldBe(AgentExecutorName.Anthropic);
        updatedAgent.Model.ShouldBe("anthropic-sonnet");
        updatedAgent.ThinkingLevel.ShouldBe(ThinkingLevel.High);
    }

    [Fact]
    public async Task ApplyBulkSwitchToExecutorAsync_WhenModelOverrideIsUnsupported_ReturnsError()
    {
        var modelRegistry = Substitute.For<IModelRegistry>();
        modelRegistry.GetModelsForExecutor(AgentExecutorName.AnthropicValue).Returns([
            new ModelDescriptor("anthropic-haiku", "Anthropic Haiku", AgentExecutorName.AnthropicValue)
        ]);

        var (viewModel, _, agentService, _) = CreateViewModel(modelRegistry: modelRegistry);
        SeedProjectWithAgent(agentService);
        viewModel.Load();

        var error = await viewModel.ApplyBulkSwitchToExecutorAsync(AgentExecutorName.AnthropicValue, "missing-model");

        error.ShouldBe("Anthropic API does not offer model 'missing-model'.");
        viewModel.BulkSwitchError.ShouldBe(error);
        viewModel.ApplyingBulkSwitch.ShouldBeFalse();
    }

    [Fact]
    public void GetAvailableModelsForExecutor_WhenExecutorHasModels_ReturnsDistinctSortedIds()
    {
        var modelRegistry = Substitute.For<IModelRegistry>();
        modelRegistry.GetModelsForExecutor(AgentExecutorName.AnthropicValue).Returns([
            new ModelDescriptor("z-model", "Z Model", AgentExecutorName.AnthropicValue),
            new ModelDescriptor("a-model", "A Model", AgentExecutorName.AnthropicValue),
            new ModelDescriptor("A-model", "A Model Duplicate", AgentExecutorName.AnthropicValue)
        ]);

        var (viewModel, _, _, _) = CreateViewModel(modelRegistry: modelRegistry);

        var models = viewModel.GetAvailableModelsForExecutor(AgentExecutorName.AnthropicValue);

        models.ShouldBe(["a-model", "z-model"]);
    }

    private (ProjectSettingsViewModel ViewModel, WorkspaceService WorkspaceService, AgentService AgentService, MainViewModel MainViewModel) CreateViewModel(
        bool withActiveProject = true,
        IModelRegistry? modelRegistry = null,
        IReadOnlyList<IAgentExecutor>? executors = null)
    {
        var workspaceService = new WorkspaceService(_paths, _fileWriter);
        var templatesService = new AgentTemplatesService(_paths);
        var effectiveModelRegistry = modelRegistry ?? Substitute.For<IModelRegistry>();
        if (modelRegistry is null)
            effectiveModelRegistry.GetModelsForExecutor(Arg.Any<string>()).Returns([]);

        var agentService = new AgentService(
            _paths,
            templatesService,
            _fileWriter,
            _coordinator,
            effectiveModelRegistry,
            NullLogger<AgentService>.Instance);

        var dispatcher = Substitute.For<IDomainEventDispatcher>();
        dispatcher.Dispatch(Arg.Any<IReadOnlyList<DomainEvent>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<Unit>>(new Ok<Unit>(Unit.Value)));

        var decisionsService = new DecisionsService(_paths, dispatcher, _fileWriter, _coordinator, NullLogger<DecisionsService>.Instance);
        var messagesService = new MessagesService(_paths);
        var healthMonitor = new ExecutorHealthMonitor(executors ?? [], NullLogger<ExecutorHealthMonitor>.Instance);
        var mainViewModel = new MainViewModel(healthMonitor, messagesService, decisionsService);

        if (withActiveProject)
        {
            workspaceService.CreateProject("demo-project", "Demo Project", "Test project").ShouldBeOfType<Ok<Unit>>();
            mainViewModel.ActiveProject = workspaceService.GetProject(new ProjectSlug("demo-project"));
        }

        var viewModel = new ProjectSettingsViewModel(
            workspaceService,
            agentService,
            templatesService,
            healthMonitor,
            effectiveModelRegistry,
            mainViewModel);

        return (viewModel, workspaceService, agentService, mainViewModel);
    }

    private void SeedProjectWithAgent(AgentService agentService)
    {
        var templatesService = new AgentTemplatesService(_paths);
        templatesService.CreateTemplate(new AgentTemplate
        {
            Slug = new AgentSlug("generic-standard"),
            Name = "Generic",
            Role = "Implement features",
            Model = "claude-sonnet-4-6",
            Executor = AgentExecutorName.ClaudeValue,
            Description = "General purpose",
            Content = "# Generic",
            ThinkingLevel = ThinkingLevel.High,
        });

        var projectSlug = new ProjectSlug("demo-project");
        var createResult = agentService.CreateAgent(projectSlug, "dev-alex", "Alex", "generic-standard");
        createResult.ShouldBeOfType<Ok<Unit>>();

        var saveResult = agentService.SaveAgentMeta(
            projectSlug,
            new AgentSlug("dev-alex"),
            "Alex",
            "General purpose",
            "claude-sonnet-4-6",
            AgentExecutorName.Claude,
            ["git-read"],
            ThinkingLevel.High);

        saveResult.ShouldBeOfType<Ok<Unit>>();
    }
}