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

    // ── Editable meta ───────────────────────────────────────────────────────
    [ObservableProperty] public partial string EditName { get; set; } = "";
    [ObservableProperty] public partial string EditDescription { get; set; } = "";
    [ObservableProperty] public partial string EditModel { get; set; } = "";
    [ObservableProperty] public partial string EditExecutor { get; set; } = "";

    // ── CLAUDE.md ───────────────────────────────────────────────────────────
    [ObservableProperty] public partial string ClaudeContent { get; set; } = "";
    [ObservableProperty] public partial bool ClaudeExpanded { get; set; }

    public ObservableCollection<MessageItem> Inbox { get; } = [];
    public ObservableCollection<string> AvailableExecutors { get; } = [];

    public event Action? NavigateToTranscript;

    public AgentDetailViewModel(
        AgentService agentService,
        AgentRunnerService agentRunnerService,
        MessagesService messagesService,
        ExecutorHealthMonitor healthMonitor,
        MainViewModel mainViewModel)
    {
        _agentService = agentService;
        _agentRunnerService = agentRunnerService;
        _messagesService = messagesService;
        _healthMonitor = healthMonitor;
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
            ClaudeContent = _agentService.GetClaudeMd(CurrentSlug, agentSlug);
            IsRunning = _agentRunnerService.IsRunning(CurrentSlug, agentSlug);

            PopulateExecutorList();
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
            EditName, EditDescription, EditModel, executorName);

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
    public void ViewTranscript() => NavigateToTranscript?.Invoke();

    public void Dispose()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
    }
}
