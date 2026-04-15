using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class DecisionsViewModel : ObservableObject
{
    private readonly DecisionsService _decisionsService;
    private readonly DecisionChatService _chatService;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial DecisionItem? SelectedDecision { get; set; }

    [ObservableProperty]
    public partial bool ShowResolved { get; set; }

    [ObservableProperty]
    public partial string ReplyText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsSendingReply { get; set; }
    public ObservableCollection<DecisionItem> Decisions { get; } = [];
    public ObservableCollection<DecisionChatMessage> ChatMessages { get; } = [];

    public DecisionsViewModel(
        DecisionsService decisionsService,
        DecisionChatService chatService,
        MainViewModel mainViewModel)
    {
        _decisionsService = decisionsService;
        _chatService = chatService;
        _mainViewModel = mainViewModel;
    }

    private ProjectSlug? CurrentSlug => _mainViewModel.ActiveProject?.Slug;

    [RelayCommand]
    public Task LoadAsync()
    {
        if (CurrentSlug is null) return Task.CompletedTask;
        IsLoading = true;
        try
        {
            var pending = _decisionsService.ListDecisions(CurrentSlug, "pending");
            Decisions.Clear();
            foreach (var d in pending) Decisions.Add(d);

            if (ShowResolved)
            {
                var resolved = _decisionsService.ListDecisions(CurrentSlug, "resolved");
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
        var messages = _chatService.GetMessages(CurrentSlug, decision.Id);
        ChatMessages.Clear();
        foreach (var m in messages) ChatMessages.Add(m);
        return Task.CompletedTask;
    }

    [RelayCommand]
    public async Task SendReplyAsync()
    {
        if (CurrentSlug is null || SelectedDecision is null || string.IsNullOrWhiteSpace(ReplyText)) return;
        IsSendingReply = true;
        try
        {
            _chatService.SendHumanMessage(CurrentSlug, SelectedDecision.Id, "user", ReplyText.Trim());
            ReplyText = "";
            await SelectDecisionAsync(SelectedDecision);
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
        await _decisionsService.ResolveDecisionAsync(CurrentSlug, SelectedDecision.Id, ReplyText.Trim());
        ReplyText = "";
        SelectedDecision = null;
        ChatMessages.Clear();
        await LoadAsync();
    }
}
