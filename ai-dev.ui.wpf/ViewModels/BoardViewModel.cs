using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using System.Collections.ObjectModel;

namespace AiDev.Desktop.ViewModels;

public partial class BoardColumnViewModel : ObservableObject
{
    public BoardColumn Column { get; }
    public ObservableCollection<BoardTask> Tasks { get; } = [];

    public BoardColumnViewModel(BoardColumn column)
    {
        Column = column;
    }
}

public partial class BoardViewModel : ObservableObject
{
    private readonly BoardService _boardService;
    private readonly AgentService _agentService;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private BoardTask? _selectedTask;
    [ObservableProperty] private string _newTaskTitle = "";
    [ObservableProperty] private string _newTaskDescription = "";
    [ObservableProperty] private string _newTaskAssignee = "";
    [ObservableProperty] private string _newTaskPriority = "normal";
    [ObservableProperty] private string _addingToColumn = "";

    public ObservableCollection<BoardColumnViewModel> Columns { get; } = [];
    public ObservableCollection<AgentInfo> Agents { get; } = [];

    public BoardViewModel(
        BoardService boardService,
        AgentService agentService,
        MainViewModel mainViewModel)
    {
        _boardService = boardService;
        _agentService = agentService;
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
            var board = _boardService.LoadBoard(CurrentSlug);
            var agents = _agentService.ListAgents(CurrentSlug);

            Agents.Clear();
            foreach (var a in agents) Agents.Add(a);

            Columns.Clear();
            foreach (var col in board.Columns)
            {
                var colVm = new BoardColumnViewModel(col);
                foreach (var taskId in col.TaskIds)
                    if (board.Tasks.TryGetValue(taskId, out var task))
                        colVm.Tasks.Add(task);
                Columns.Add(colVm);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void ShowAddTask(string columnId)
    {
        AddingToColumn = columnId;
        NewTaskTitle = "";
        NewTaskDescription = "";
        NewTaskAssignee = "";
        NewTaskPriority = "normal";
    }

    [RelayCommand]
    public async Task CreateTaskAsync()
    {
        if (CurrentSlug is null || string.IsNullOrWhiteSpace(NewTaskTitle) || string.IsNullOrWhiteSpace(AddingToColumn)) return;

        await _boardService.CreateTaskAsync(
            CurrentSlug,
            AddingToColumn,
            NewTaskTitle.Trim(),
            NewTaskDescription.Trim() is { Length: > 0 } d ? d : null,
            NewTaskPriority,
            string.IsNullOrWhiteSpace(NewTaskAssignee) ? null : NewTaskAssignee);

        AddingToColumn = "";
        NewTaskTitle = "";
        NewTaskDescription = "";
        await LoadAsync();
    }

    [RelayCommand]
    public async Task DeleteTaskAsync(BoardTask task)
    {
        if (CurrentSlug is null) return;
        await _boardService.DeleteTaskAsync(CurrentSlug, task.Id);
        await LoadAsync();
    }
}
