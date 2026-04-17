using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class TaskTranscriptViewModel : ObservableObject
{
    private readonly BoardService _boardService;
    private readonly MessagesService _messagesService;
    private readonly MainViewModel _mainViewModel;

    private BoardTask? _task;
    public BoardTask? Task
    {
        get => _task;
        set => SetProperty(ref _task, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private string _errorText = string.Empty;
    public string ErrorText
    {
        get => _errorText;
        set => SetProperty(ref _errorText, value);
    }

    public ObservableCollection<MessageItem> Messages { get; } = [];

    public event Action? NavigateBack;

    public TaskTranscriptViewModel(
        BoardService boardService,
        MessagesService messagesService,
        MainViewModel mainViewModel)
    {
        _boardService = boardService;
        _messagesService = messagesService;
        _mainViewModel = mainViewModel;
    }

    private ProjectSlug? CurrentSlug => _mainViewModel.ActiveProject?.Slug;

    [RelayCommand]
    private void GoBack() => NavigateBack?.Invoke();

    public Task LoadAsync(TaskId taskId)
    {
        if (CurrentSlug is null)
            return System.Threading.Tasks.Task.CompletedTask;

        IsLoading = true;
        ErrorText = string.Empty;
        Messages.Clear();

        try
        {
            var board = _boardService.LoadBoard(CurrentSlug);
            if (!board.Tasks.TryGetValue(taskId, out var task))
            {
                Task = null;
                ErrorText = "Task not found.";
                return System.Threading.Tasks.Task.CompletedTask;
            }

            Task = task;

            var messages = _messagesService.ListMessages(CurrentSlug)
                .Where(message => message.TaskId == taskId)
                .OrderByDescending(message => message.Date)
                .ThenByDescending(message => message.Filename)
                .ToList();

            foreach (var message in messages)
                Messages.Add(message);

            return System.Threading.Tasks.Task.CompletedTask;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
