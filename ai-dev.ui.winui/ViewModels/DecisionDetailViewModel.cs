using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class DecisionDetailViewModel : ObservableObject, IDisposable
{
    private readonly DecisionsService _decisionsService;
    private readonly DecisionChatService _chatService;
    private readonly AgentRunnerService _runnerService;
    private readonly MainViewModel _mainViewModel;
    private readonly DispatcherQueue _dispatcher;
    private Timer? _pollTimer;

    [ObservableProperty] public partial DecisionItem? Decision { get; set; }
    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial string ChatInput { get; set; } = "";
    [ObservableProperty] public partial string ChatError { get; set; } = "";
    [ObservableProperty] public partial bool IsSendingChat { get; set; }
    [ObservableProperty] public partial bool ShowResolveDialog { get; set; }
    [ObservableProperty] public partial string ResolveResponse { get; set; } = "";
    [ObservableProperty] public partial string ResolveError { get; set; } = "";
    [ObservableProperty] public partial bool IsResolving { get; set; }
    [ObservableProperty] public partial bool AgentIsRunning { get; set; }

    public ObservableCollection<DecisionChatMessage> ChatMessages { get; } = [];

    public event Action? NavigateBack;

    public DecisionDetailViewModel(
        DecisionsService decisionsService,
        DecisionChatService chatService,
        AgentRunnerService runnerService,
        MainViewModel mainViewModel)
    {
        _decisionsService = decisionsService;
        _chatService = chatService;
        _runnerService = runnerService;
        _mainViewModel = mainViewModel;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    private ProjectSlug? CurrentSlug => _mainViewModel.ActiveProject?.Slug;

    public void Load(string decisionId)
    {
        if (CurrentSlug is null) return;
        IsLoading = true;
        try
        {
            Decision = _decisionsService.GetDecision(CurrentSlug, decisionId);
            if (Decision is null) return;

            RefreshChat(decisionId);
            RefreshAgentRunning();

            _pollTimer?.Dispose();
            if (Decision.Status.IsPending)
            {
                _pollTimer = new Timer(_ => _dispatcher.TryEnqueue(() =>
                {
                    if (Decision is null) return;
                    Decision = _decisionsService.GetDecision(CurrentSlug!, Decision.Id);
                    RefreshChat(Decision?.Id ?? decisionId);
                    RefreshAgentRunning();
                }), null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
            }
        }
        finally { IsLoading = false; }
    }

    private void RefreshChat(string decisionId)
    {
        if (CurrentSlug is null) return;
        var msgs = _chatService.GetMessages(CurrentSlug, decisionId);
        ChatMessages.Clear();
        foreach (var m in msgs) ChatMessages.Add(m);
    }

    private void RefreshAgentRunning()
    {
        if (Decision is null || CurrentSlug is null) return;
        if (AgentSlug.TryParse(Decision.From, out var agentSlug))
            AgentIsRunning = _runnerService.IsRunning(CurrentSlug, agentSlug);
        else
            AgentIsRunning = false;
    }

    [RelayCommand]
    public void SendChat()
    {
        if (CurrentSlug is null || Decision is null || string.IsNullOrWhiteSpace(ChatInput)) return;
        ChatError = "";
        var err = _chatService.SendHumanMessage(CurrentSlug, Decision.Id, Decision.From, ChatInput.Trim());
        if (err != null) { ChatError = err; return; }
        ChatInput = "";
        RefreshChat(Decision.Id);
        RefreshAgentRunning();
    }

    [RelayCommand]
    public void StartResolve()
    {
        ResolveResponse = "";
        ResolveError = "";
        ShowResolveDialog = true;
    }

    [RelayCommand]
    public void CancelResolve() => ShowResolveDialog = false;

    [RelayCommand]
    public async Task ConfirmResolveAsync()
    {
        if (CurrentSlug is null || Decision is null || string.IsNullOrWhiteSpace(ResolveResponse)) return;
        IsResolving = true;
        ResolveError = "";
        try
        {
            var result = await _decisionsService.ResolveDecisionAsync(CurrentSlug, Decision.Id, ResolveResponse.Trim());
            if (result is Err<Unit> err) { ResolveError = err.Error.Message; return; }
            ShowResolveDialog = false;
            _pollTimer?.Dispose();
            Decision = _decisionsService.GetDecision(CurrentSlug, Decision.Id);
        }
        finally { IsResolving = false; }
    }

    [RelayCommand]
    public void GoBack() => NavigateBack?.Invoke();

    public void Dispose()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
    }
}
