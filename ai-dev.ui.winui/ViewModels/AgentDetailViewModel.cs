using AiDev.Executors;
using AiDev.Features.Agent;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.UI.Dispatching;

using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class AgentDetailViewModel : ObservableObject, IDisposable
{
    private readonly AgentService _agentService;
    private readonly AgentRunnerService _agentRunnerService;
    private readonly MessagesService _messagesService;
    private readonly ExecutorHealthMonitor _healthMonitor;
    private readonly IModelRegistry _modelRegistry;
    private readonly MainViewModel _mainViewModel;
    private readonly DispatcherQueue _dispatcher;
    private Timer? _pollTimer;

    // ── Core state ──────────────────────────────────────────────────────────
    [ObservableProperty] public partial AgentInfo? Agent { get; set; }
    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial bool IsRunning { get; set; }
    [ObservableProperty] public partial string SaveError { get; set; } = "";
    [ObservableProperty] public partial bool MetaSaved { get; set; }
    [ObservableProperty] public partial bool ClaudeSaved { get; set; }
    [ObservableProperty] public partial TokenUsage? LastSessionUsage { get; set; }

    // ── Editable meta ───────────────────────────────────────────────────────
    [ObservableProperty] public partial string EditName { get; set; } = "";
    [ObservableProperty] public partial string EditDescription { get; set; } = "";
    [ObservableProperty] public partial string EditModel { get; set; } = "";
    [ObservableProperty] public partial string EditExecutor { get; set; } = "";
    [ObservableProperty] public partial ThinkingLevel EditThinkingLevel { get; set; } = ThinkingLevel.Off;
    [ObservableProperty] public partial bool ModelSupportsThinking { get; set; }
    [ObservableProperty] public partial bool HasSkills { get; set; }

    public ObservableCollection<SkillSelectionItem> SkillSelections { get; } = [];
    public IReadOnlyList<ThinkingLevel> AvailableThinkingLevels { get; } =
        [ThinkingLevel.Off, ThinkingLevel.Low, ThinkingLevel.Medium, ThinkingLevel.High];

    // ── CLAUDE.md ───────────────────────────────────────────────────────────
    [ObservableProperty] public partial string ClaudeContent { get; set; } = "";
    [ObservableProperty] public partial bool ClaudeExpanded { get; set; }

    public ObservableCollection<MessageItem> Inbox { get; } = [];
    public ObservableCollection<string> AvailableExecutors { get; } = [];
    public ObservableCollection<string> AvailableModels { get; } = [];

    public event Action? NavigateBack;
    public event Action? NavigateToTranscript;

    public AgentDetailViewModel(
        AgentService agentService,
        AgentRunnerService agentRunnerService,
        MessagesService messagesService,
        ExecutorHealthMonitor healthMonitor,
        IModelRegistry modelRegistry,
        MainViewModel mainViewModel)
    {
        _agentService = agentService;
        _agentRunnerService = agentRunnerService;
        _messagesService = messagesService;
        _healthMonitor = healthMonitor;
        _modelRegistry = modelRegistry;
        _mainViewModel = mainViewModel;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    private ProjectSlug? CurrentSlug => _mainViewModel.ActiveProject?.Slug;

    public async Task LoadAsync(AgentSlug agentSlug)
    {
        if (CurrentSlug is null) return;
        IsLoading = true;
        try
        {
            Agent = _agentService.LoadAgent(CurrentSlug, agentSlug);
            if (Agent is null) return;

            EditName = Agent.Name;
            EditDescription = Agent.Description;
            EditModel = Agent.Model ?? "";
            EditExecutor = Agent.Executor.Value;
            EditThinkingLevel = Agent.ThinkingLevel;
            ClaudeContent = _agentService.GetClaudeMd(CurrentSlug, agentSlug);
            LastSessionUsage = _agentRunnerService.GetLastSessionUsage(CurrentSlug, agentSlug);
            IsRunning = _agentRunnerService.IsRunning(CurrentSlug, agentSlug);

            PopulateExecutorList();
            PopulateModelList();
            PopulateSkillsList();
            UpdateModelCapabilities();
            RefreshInbox(agentSlug);

            // Poll every 2 s for live run-state changes
            _pollTimer?.Dispose();
            _pollTimer = new Timer(_ =>
            {
                _dispatcher.TryEnqueue(() =>
                {
                    if (Agent is null) return;
                    var running = _agentRunnerService.IsRunning(CurrentSlug!, Agent.Slug);
                    if (running != IsRunning)
                    {
                        IsRunning = running;
                        Agent = _agentService.LoadAgent(CurrentSlug!, Agent.Slug);
                        if (Agent is not null) RefreshInbox(Agent.Slug);
                    }
                });
            }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RefreshInbox(AgentSlug agentSlug)
    {
        if (CurrentSlug is null) return;
        var messages = _messagesService.ListMessages(CurrentSlug, agentSlug);
        Inbox.Clear();
        foreach (var m in messages) Inbox.Add(m);
    }

    private void PopulateExecutorList()
    {
        AvailableExecutors.Clear();
        foreach (var executor in AgentExecutorName.Supported)
            AvailableExecutors.Add(executor.Value);
    }

    private void PopulateModelList()
    {
        AvailableModels.Clear();

        if (string.IsNullOrWhiteSpace(EditExecutor))
            return;

        foreach (var model in _modelRegistry.GetModelsForExecutor(EditExecutor))
            AvailableModels.Add(model.Id);

        if (AvailableModels.Count > 0 && !AvailableModels.Contains(EditModel, StringComparer.OrdinalIgnoreCase))
            EditModel = AvailableModels[0];
    }

    private void PopulateSkillsList()
    {
        SkillSelections.Clear();

        if (Agent is null || string.IsNullOrWhiteSpace(EditExecutor))
        {
            HasSkills = false;
            return;
        }

        var health = _healthMonitor.GetExecutorHealth()
            .FirstOrDefault(e => e.Executor.Name == EditExecutor);

        if (health.Executor != null)
        {
            foreach (var skill in health.Executor.AvailableSkills)
            {
                SkillSelections.Add(new SkillSelectionItem
                {
                    Key = skill.Key,
                    DisplayName = skill.DisplayName,
                    Description = skill.Description,
                    IsEnabled = Agent.Skills.Contains(skill.Key)
                });
            }
        }

        HasSkills = SkillSelections.Count > 0;
    }

    private void UpdateModelCapabilities()
    {
        var descriptor = _modelRegistry.Find(EditExecutor, EditModel);
        ModelSupportsThinking = descriptor?.Capabilities.HasFlag(ModelCapabilities.Reasoning) == true;
    }

    partial void OnEditExecutorChanged(string value)
    {
        PopulateModelList();
        PopulateSkillsList();
        UpdateModelCapabilities();
    }

    partial void OnEditModelChanged(string value)
    {
        UpdateModelCapabilities();
    }

    [RelayCommand]
    public void SaveMeta()
    {
        if (CurrentSlug is null || Agent is null) return;
        SaveError = "";
        MetaSaved = false;

        if (!AgentExecutorName.TryParse(EditExecutor, out var executorName))
            executorName = AgentExecutorName.Default;

        var result = _agentService.SaveAgentMeta(
            CurrentSlug, Agent.Slug,
            EditName, EditDescription, EditModel, executorName,
            [.. SkillSelections.Where(s => s.IsEnabled).Select(s => s.Key)],
            EditThinkingLevel);

        if (result is Err<Unit> err)
        {
            SaveError = err.Error.Message;
            return;
        }

        Agent = _agentService.LoadAgent(CurrentSlug, Agent.Slug);
        MetaSaved = true;
        _ = Task.Delay(2000).ContinueWith(_ =>
            _dispatcher.TryEnqueue(() => MetaSaved = false));
    }

    [RelayCommand]
    public void SaveClaude()
    {
        if (CurrentSlug is null || Agent is null) return;
        ClaudeSaved = false;
        var result = _agentService.SaveClaudeMd(CurrentSlug, Agent.Slug, ClaudeContent);
        if (result is Ok<Unit>)
        {
            ClaudeSaved = true;
            _ = Task.Delay(2000).ContinueWith(_ =>
                _dispatcher.TryEnqueue(() => ClaudeSaved = false));
        }
    }

    [RelayCommand]
    public void Run()
    {
        if (CurrentSlug is null || Agent is null) return;
        _agentRunnerService.LaunchAgent(CurrentSlug, Agent.Slug);
        IsRunning = true;
        Agent = _agentService.LoadAgent(CurrentSlug, Agent.Slug);
    }

    [RelayCommand]
    public void Stop()
    {
        if (CurrentSlug is null || Agent is null) return;
        _agentRunnerService.StopAgent(CurrentSlug, Agent.Slug);
        IsRunning = false;
    }

    [RelayCommand]
    public void GoBack() => NavigateBack?.Invoke();

    [RelayCommand]
    public void ViewTranscript() => NavigateToTranscript?.Invoke();

    public void Dispose()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
    }
}
