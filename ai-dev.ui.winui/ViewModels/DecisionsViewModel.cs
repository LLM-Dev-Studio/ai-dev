using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class DecisionsViewModel : ObservableObject
{
    private readonly DecisionsService _decisionsService;
    private readonly DecisionChatService _chatService;
    private readonly MainViewModel _mainViewModel;
    private readonly IUiDispatcher _uiDispatcher;
    private Timer? _pollTimer;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial DecisionItem? SelectedDecision { get; set; }

    [ObservableProperty]
    public partial bool ShowResolved { get; set; }

    partial void OnShowResolvedChanged(bool value) => _ = LoadAsync();

    [ObservableProperty]
    public partial string ReplyText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsSendingReply { get; set; }
    public ObservableCollection<DecisionItem> Decisions { get; } = [];
    public ObservableCollection<DecisionChatMessage> ChatMessages { get; } = [];

    public DecisionsViewModel(
        DecisionsService decisionsService,
        DecisionChatService chatService,
        MainViewModel mainViewModel,
        IUiDispatcher uiDispatcher)
    {
        _decisionsService = decisionsService;
        _chatService = chatService;
        _mainViewModel = mainViewModel;
        _uiDispatcher = uiDispatcher;
    }

    private ProjectSlug? CurrentSlug => _mainViewModel.ActiveProject?.Slug;

    [RelayCommand]
    public Task LoadAsync()
    {
        if (CurrentSlug is null) return Task.CompletedTask;
        IsLoading = true;
        try
        {
            var pending = _decisionsService.ListDecisions(CurrentSlug, DecisionStatus.Pending);
            Decisions.Clear();
            foreach (var d in pending) Decisions.Add(d);

            if (ShowResolved)
            {
                var resolved = _decisionsService.ListDecisions(CurrentSlug, DecisionStatus.Resolved);
                foreach (var d in resolved) Decisions.Add(d);
            }
            return Task.CompletedTask;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public Task SelectDecisionAsync(DecisionItem decision)
    {
        if (CurrentSlug is null) return Task.CompletedTask;
        SelectedDecision = decision;
        RefreshChat(decision);
        StartPollingDecision(decision);
        return Task.CompletedTask;
    }

    [RelayCommand]
    public async Task SendReplyAsync()
    {
        if (CurrentSlug is null || SelectedDecision is null || string.IsNullOrWhiteSpace(ReplyText)) return;
        IsSendingReply = true;
        try
        {
            _chatService.SendHumanMessage(CurrentSlug, SelectedDecision.Id, SelectedDecision.From, ReplyText.Trim());
            ReplyText = "";
            await PollSelectedDecisionAsync();
        }
        finally
        {
            IsSendingReply = false;
        }
    }

    [RelayCommand]
    public async Task ResolveDecisionAsync()
    {
        if (CurrentSlug is null || SelectedDecision is null) return;
        StopPollingDecision();
        await _decisionsService.ResolveDecisionAsync(CurrentSlug, SelectedDecision.Id, ReplyText.Trim());
        ReplyText = "";
        SelectedDecision = null;
        ChatMessages.Clear();
        await LoadAsync();
        _mainViewModel.RefreshNavBadges();
    }

    public void StopPollingDecision()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    private void RefreshChat(DecisionItem decision)
    {
        if (CurrentSlug is null) return;
        var messages = _chatService.GetMessages(CurrentSlug, decision.Id);
        ChatMessages.Clear();
        foreach (var m in messages) ChatMessages.Add(m);
    }

    private void StartPollingDecision(DecisionItem decision)
    {
        StopPollingDecision();
        if (!decision.Status.IsPending) return;

        _pollTimer = new Timer(_ => _uiDispatcher.Enqueue(() => _ = PollSelectedDecisionAsync()),
            null,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(3));
    }

    private async Task PollSelectedDecisionAsync()
    {
        if (CurrentSlug is null || SelectedDecision is null) return;

        _chatService.FlushAgentReplies(CurrentSlug, SelectedDecision.Id, SelectedDecision.From);

        var latest = _decisionsService.GetDecision(CurrentSlug, SelectedDecision.Id);
        if (latest is null)
        {
            StopPollingDecision();
            SelectedDecision = null;
            ChatMessages.Clear();
            await LoadAsync();
            return;
        }

        // Replace the item in Decisions with the fresh fetch before assigning SelectedDecision.
        // The ListView's TwoWay SelectedItem binding uses reference equality; setting SelectedDecision
        // to a new object not present in the collection coerces SelectedItem to null, which fires back
        // through the binding and clears SelectedDecision — silently breaking subsequent sends.
        var idx = -1;
        for (var i = 0; i < Decisions.Count; i++)
            if (Decisions[i].Id == latest.Id) { idx = i; break; }
        if (idx >= 0) Decisions[idx] = latest;

        SelectedDecision = latest;
        RefreshChat(latest);

        if (!latest.Status.IsPending)
        {
            StopPollingDecision();
            await LoadAsync();
            _mainViewModel.RefreshNavBadges();
        }
    }
}
