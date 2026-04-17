using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class MessagesViewModel : ObservableObject
{
    private readonly MessagesService _messagesService;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial MessageItem? SelectedMessage { get; set; }

    [ObservableProperty]
    public partial bool ShowProcessed { get; set; }

    [ObservableProperty]
    public partial string FilterTaskId { get; set; } = "";

    public ObservableCollection<MessageItem> Messages { get; } = [];

    public MessagesViewModel(MessagesService messagesService, MainViewModel mainViewModel)
    {
        _messagesService = messagesService;
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
            var messages = _messagesService.ListMessages(CurrentSlug);
            Messages.Clear();
            var filter = FilterTaskId.Trim();
            foreach (var m in messages.Where(m =>
                (ShowProcessed || !m.IsProcessed) &&
                (string.IsNullOrEmpty(filter) || string.Equals(m.TaskId?.Value, filter, StringComparison.OrdinalIgnoreCase))))
                Messages.Add(m);
            return Task.CompletedTask;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task MarkProcessedAsync(MessageItem message)
    {
        if (CurrentSlug is null || message.IsProcessed) return;
        try
        {
            _messagesService.MarkProcessed(CurrentSlug, message.AgentSlug, message.Filename);
            SelectedMessage = null;
            await LoadAsync();
            _mainViewModel.RefreshNavBadges();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to mark message as processed: {ex.Message}");
        }
    }
}
