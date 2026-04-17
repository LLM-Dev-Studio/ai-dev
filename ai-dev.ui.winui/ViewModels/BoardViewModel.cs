using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.UI.Dispatching;

using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class BoardColumnViewModel : ObservableObject
{
    public BoardColumn Column { get; }
    public ObservableCollection<BoardTask> Tasks { get; } = [];

    public BoardColumnViewModel(BoardColumn column)
    {
        Column = column;
    }
}

public sealed record AssigneeOption(string DisplayName, string Value);

public partial class BoardViewModel : ObservableObject, IDisposable
{
    private readonly BoardService _boardService;
    private readonly AgentService _agentService;
    private readonly PromptEnhancerService _enhancerService;
    private readonly MainViewModel _mainViewModel;
    private readonly DispatcherQueue _dispatcher;
    private Timer? _pollTimer;

    [ObservableProperty] public partial bool IsLoading { get; set; }

    // ── Task dialog state ───────────────────────────────────────────────────
    [ObservableProperty] public partial bool ShowTaskDialog { get; set; }
    [ObservableProperty] public partial bool IsEditing { get; set; }
    [ObservableProperty] public partial string DialogTitle { get; set; } = "New task";
    [ObservableProperty] public partial string TaskTitle { get; set; } = "";
    [ObservableProperty] public partial string TaskDescription { get; set; } = "";
    [ObservableProperty] public partial string TaskPriority { get; set; } = "normal";
    [ObservableProperty] public partial string TaskColumnId { get; set; } = "";
    [ObservableProperty] public partial string TaskAssignee { get; set; } = "";
    [ObservableProperty] public partial AssigneeOption? SelectedAssigneeOption { get; set; }
    [ObservableProperty] public partial string TaskError { get; set; } = "";
    [ObservableProperty] public partial bool IsEnhancing { get; set; }
    [ObservableProperty] public partial bool IsSavingTask { get; set; }

    private TaskId? _editingTaskId;
    private CancellationTokenSource? _enhanceCts;

    public ObservableCollection<BoardColumnViewModel> Columns { get; } = [];
    public ObservableCollection<AgentInfo> Agents { get; } = [];
    public ObservableCollection<AssigneeOption> AssigneeOptions { get; } = [];

    public BoardViewModel(
        BoardService boardService,
        AgentService agentService,
        PromptEnhancerService enhancerService,
        MainViewModel mainViewModel)
    {
        _boardService = boardService;
        _agentService = agentService;
        _enhancerService = enhancerService;
        _mainViewModel = mainViewModel;
        // Capture the dispatcher on the UI thread so background timer callbacks can marshal to it.
        _dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    private ProjectSlug? CurrentSlug => _mainViewModel.ActiveProject?.Slug;

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (CurrentSlug is null) return;
        IsLoading = true;
        try
        {
            RefreshAgents();
            RefreshBoard();

            // Poll every 3 s for board changes from running agents
            _pollTimer ??= new Timer(_ =>
                _dispatcher.TryEnqueue(RefreshBoard),
                null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RefreshBoard()
    {
        if (CurrentSlug is null) return;
        var board = _boardService.LoadBoard(CurrentSlug);

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

    private void RefreshAgents()
    {
        if (CurrentSlug is null) return;

        var agents = _agentService.ListAgents(CurrentSlug);
        Agents.Clear();
        foreach (var a in agents) Agents.Add(a);

        AssigneeOptions.Clear();
        AssigneeOptions.Add(new AssigneeOption("Unassigned", string.Empty));
        foreach (var a in Agents)
            AssigneeOptions.Add(new AssigneeOption($"{a.Name} ({a.Role})", a.Slug.Value));

        SyncSelectedAssigneeOption();
    }

    /// <summary>Opens the dialog for a new task in the specified column.</summary>
    public void OpenNewTask(string columnId)
    {
        _editingTaskId = null;
        IsEditing = false;
        DialogTitle = "New task";
        TaskTitle = "";
        TaskDescription = "";
        TaskPriority = "normal";
        TaskColumnId = columnId;
        TaskAssignee = GetDefaultAssignee();
        SyncSelectedAssigneeOption();
        TaskError = "";
        ShowTaskDialog = true;
    }

    /// <summary>Opens the dialog to edit an existing task.</summary>
    public void OpenEditTask(BoardTask task, string columnId)
    {
        _editingTaskId = task.Id;
        IsEditing = true;
        DialogTitle = "Edit task";
        TaskTitle = task.Title;
        TaskDescription = task.Description ?? "";
        TaskPriority = task.Priority.Value;
        TaskColumnId = columnId;
        TaskAssignee = task.Assignee ?? "";
        SyncSelectedAssigneeOption();
        TaskError = "";
        ShowTaskDialog = true;
    }

    [RelayCommand]
    public void CloseTaskDialog()
    {
        ShowTaskDialog = false;
        _editingTaskId = null;
        IsEditing = false;
    }

    [RelayCommand]
    public async Task EnhanceAsync()
    {
        if (CurrentSlug is null) return;
        _enhanceCts?.Dispose();
        _enhanceCts = new CancellationTokenSource();
        IsEnhancing = true;
        try
        {
            var result = await _enhancerService.EnhanceAsync(CurrentSlug, TaskTitle, TaskDescription, _enhanceCts.Token);
            if (result is not null)
            {
                TaskTitle = result.Title;
                TaskDescription = result.Description;
            }
        }
        finally
        {
            IsEnhancing = false;
            _enhanceCts?.Dispose();
            _enhanceCts = null;
        }
    }

    [RelayCommand]
    public void CancelEnhance()
    {
        _enhanceCts?.Cancel();
    }

    [RelayCommand]
    public async Task SaveTaskAsync()
    {
        if (CurrentSlug is null || string.IsNullOrWhiteSpace(TaskTitle)) return;
        IsSavingTask = true;
        TaskError = "";
        try
        {
            Result<BoardTask> result;
            if (_editingTaskId is null)
            {
                result = await _boardService.CreateTaskAsync(
                    CurrentSlug, TaskColumnId,
                    TaskTitle.Trim(),
                    TaskDescription.Trim() is { Length: > 0 } d ? d : null,
                    TaskPriority,
                    string.IsNullOrWhiteSpace(TaskAssignee) ? null : TaskAssignee);
            }
            else
            {
                result = await _boardService.UpdateTaskAsync(
                    CurrentSlug, _editingTaskId,
                    TaskColumnId,
                    TaskTitle.Trim(),
                    TaskDescription.Trim() is { Length: > 0 } d ? d : null,
                    TaskPriority,
                    string.IsNullOrWhiteSpace(TaskAssignee) ? null : TaskAssignee);
            }

            if (result is Err<BoardTask> err)
            {
                TaskError = err.Error.Message;
                return;
            }

            ShowTaskDialog = false;
            _editingTaskId = null;
            IsEditing = false;
            RefreshBoard();
        }
        finally
        {
            IsSavingTask = false;
        }
    }

    [RelayCommand]
    public async Task DeleteTaskAsync()
    {
        if (CurrentSlug is null || _editingTaskId is null) return;
        await _boardService.DeleteTaskAsync(CurrentSlug, _editingTaskId);
        ShowTaskDialog = false;
        _editingTaskId = null;
        IsEditing = false;
        RefreshBoard();
    }

    private string GetDefaultAssignee()
    {
        var pm = Agents.FirstOrDefault(a => string.Equals(a.Role, "PM", StringComparison.OrdinalIgnoreCase))
                 ?? Agents.FirstOrDefault(a => a.Role.Contains("pm", StringComparison.OrdinalIgnoreCase));

        return pm is null ? string.Empty : pm.Slug.Value;
    }

    partial void OnSelectedAssigneeOptionChanged(AssigneeOption? value)
    {
        var selected = value?.Value ?? string.Empty;
        if (!string.Equals(TaskAssignee, selected, StringComparison.Ordinal))
            TaskAssignee = selected;
    }

    partial void OnTaskAssigneeChanged(string value)
    {
        var selected = SelectedAssigneeOption?.Value ?? string.Empty;
        if (!string.Equals(selected, value, StringComparison.Ordinal))
            SyncSelectedAssigneeOption();
    }

    private void SyncSelectedAssigneeOption()
    {
        var match = AssigneeOptions.FirstOrDefault(a => string.Equals(a.Value, TaskAssignee, StringComparison.Ordinal))
                    ?? AssigneeOptions.FirstOrDefault(a => string.IsNullOrEmpty(a.Value));

        if (!ReferenceEquals(SelectedAssigneeOption, match))
            SelectedAssigneeOption = match;
    }

    public void Dispose()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
    }
}
