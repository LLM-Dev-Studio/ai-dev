using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using System.Collections.ObjectModel;

namespace AiDev.Desktop.ViewModels;

public partial class DecisionsViewModel : ObservableObject
{
    private readonly DecisionsService _decisionsService;
    private readonly DecisionChatService _chatService;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private DecisionItem? _selectedDecision;
    [ObservableProperty] private bool _showResolved;
    [ObservableProperty] private string _replyText = "";
    [ObservableProperty] private bool _isSendingReply;

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
    public async Task LoadAsync()
    {
        if (CurrentSlug is null) return;
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
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SelectDecisionAsync(DecisionItem decision)
    {
        if (CurrentSlug is null) return;
        SelectedDecision = decision;
        var messages = _chatService.GetMessages(CurrentSlug, decision.Id);
        ChatMessages.Clear();
        foreach (var m in messages) ChatMessages.Add(m);
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
