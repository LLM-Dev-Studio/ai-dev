using AiDev.Features.Workspace;
using AiDev.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AiDev.Desktop.ViewModels;

public partial class MessagesViewModel : ObservableObject
{
    private readonly MessagesService _messagesService;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private MessageItem? _selectedMessage;
    [ObservableProperty] private bool _showProcessed;

    public ObservableCollection<MessageItem> Messages { get; } = [];

    public MessagesViewModel(MessagesService messagesService, MainViewModel mainViewModel)
    {
        _messagesService = messagesService;
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
            var messages = _messagesService.ListMessages(CurrentSlug);
            Messages.Clear();
            foreach (var m in messages.Where(m => ShowProcessed || !m.IsProcessed))
                Messages.Add(m);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task MarkProcessedAsync(MessageItem message)
    {
        if (CurrentSlug is null) return;
        // Messages are marked processed by moving to processed sub-dir; the service handles this
        // via AgentRunnerService or direct file operations. For now just reload.
        await LoadAsync();
    }
}
