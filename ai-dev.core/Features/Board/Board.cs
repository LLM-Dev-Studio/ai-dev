namespace AiDev.Features.Board;

public sealed class Board
{
    private static readonly DomainError UnknownColumnError = new("BOARD_UNKNOWN_COLUMN", "Column not found.");
    private static readonly DomainError TaskNotFoundError = new("BOARD_TASK_NOT_FOUND", "Task not found.");
    private static readonly DomainError DuplicateTaskError = new("BOARD_DUPLICATE_TASK", "Task already exists on the board.");
    private static readonly DomainError InvalidAssigneeError = new("BOARD_INVALID_ASSIGNEE", "Assignee must be a valid agent slug.");
    private static readonly DomainError OrphanedTaskError = new("BOARD_ORPHANED_TASK", "Task is not assigned to a board column.");

    private readonly List<BoardColumn> _columns;
    private readonly Dictionary<TaskId, BoardTask> _tasks;
    [JsonIgnore] private readonly List<DomainEvent> _domainEvents = [];
    [JsonIgnore] public ProjectSlug ProjectSlug { get; }

    public Board(ProjectSlug projectSlug, List<BoardColumn>? columns = null, Dictionary<TaskId, BoardTask>? tasks = null)
    {
        ArgumentNullException.ThrowIfNull(projectSlug);
        ProjectSlug = projectSlug;
        _columns = columns is { Count: > 0 } ? columns : CreateDefaultColumns();
        _tasks = tasks ?? new();
    }

    public IReadOnlyList<BoardColumn> Columns => _columns.AsReadOnly();
    public IReadOnlyDictionary<TaskId, BoardTask> Tasks => new System.Collections.ObjectModel.ReadOnlyDictionary<TaskId, BoardTask>(_tasks);

    /// <summary>
    /// Adds a task to the requested column and records assignment side effects.
    /// </summary>
    public Result<BoardTask> AddTask(ColumnId columnId, BoardTask task)
    {
        ArgumentNullException.ThrowIfNull(columnId);
        ArgumentNullException.ThrowIfNull(task);

        var column = _columns.FirstOrDefault(c => c.Id == columnId);
        if (column == null)
            return new Err<BoardTask>(UnknownColumnError);
        if (_tasks.ContainsKey(task.Id))
            return new Err<BoardTask>(DuplicateTaskError);
        if (!TryValidateAssignee(task.Assignee, out var assignee))
            return new Err<BoardTask>(InvalidAssigneeError);

        _tasks[task.Id] = task;
        column.AddTask(task.Id);

        if (assignee != null)
            _domainEvents.Add(new TaskAssigned(ProjectSlug, task.Id, assignee, task.Title, task.Description, task.Priority, DateTime.UtcNow));

        return new Ok<BoardTask>(task);
    }

    /// <summary>
    /// Updates a task and moves it between columns when needed.
    /// </summary>
    public Result<BoardTask> UpdateTask(
        TaskId taskId,
        ColumnId newColumnId,
        string title,
        Priority priority,
        string? description,
        string? assignee,
        DateTime movedAt)
    {
        ArgumentNullException.ThrowIfNull(taskId);
        ArgumentNullException.ThrowIfNull(newColumnId);

        if (!_tasks.TryGetValue(taskId, out var task))
            return new Err<BoardTask>(TaskNotFoundError);

        if (!TryValidateAssignee(assignee, out var parsedAssignee))
            return new Err<BoardTask>(InvalidAssigneeError);

        var currentColumn = _columns.FirstOrDefault(c => c.ContainsTask(taskId));
        if (currentColumn == null)
            return new Err<BoardTask>(OrphanedTaskError);

        var targetColumn = _columns.FirstOrDefault(c => c.Id == newColumnId);
        if (targetColumn == null)
            return new Err<BoardTask>(UnknownColumnError);

        var previousAssignee = task.Assignee;
        task.UpdateDetails(title, priority, description, assignee);

        if (currentColumn.Id != newColumnId)
        {
            currentColumn.RemoveTask(taskId);
            targetColumn.AddTask(taskId);
            task.MoveToColumn(newColumnId, movedAt);
        }

        if (!string.Equals(previousAssignee, task.Assignee, StringComparison.Ordinal) && parsedAssignee != null)
            _domainEvents.Add(new TaskAssigned(ProjectSlug, task.Id, parsedAssignee, task.Title, task.Description, task.Priority, movedAt));

        return new Ok<BoardTask>(task);
    }

    /// <summary>
    /// Marks a task as nudged.
    /// </summary>
    public Result<Unit> MarkTaskNudged(TaskId taskId, DateTime nudgedAt)
    {
        ArgumentNullException.ThrowIfNull(taskId);

        if (!_tasks.TryGetValue(taskId, out var task))
            return new Err<Unit>(TaskNotFoundError);

        task.MarkNudged(nudgedAt);
        return new Ok<Unit>(Unit.Value);
    }

    /// <summary>
    /// Removes a task from the board and any column that references it.
    /// </summary>
    public Result<Unit> DeleteTask(TaskId taskId)
    {
        ArgumentNullException.ThrowIfNull(taskId);

        if (!_tasks.Remove(taskId))
            return new Err<Unit>(TaskNotFoundError);

        foreach (var column in _columns)
            column.RemoveTask(taskId);

        return new Ok<Unit>(Unit.Value);
    }

    /// <summary>
    /// Drains pending domain events raised by board operations.
    /// </summary>
    public IReadOnlyList<DomainEvent> DequeueDomainEvents()
    {
        if (_domainEvents.Count == 0)
            return [];

        var events = _domainEvents.ToArray();
        _domainEvents.Clear();
        return events;
    }

    private static bool TryValidateAssignee(string? assignee, out AgentSlug? agentSlug)
    {
        if (string.IsNullOrWhiteSpace(assignee))
        {
            agentSlug = null;
            return true;
        }

        return AgentSlug.TryParse(assignee, out agentSlug);
    }

    private static List<BoardColumn> CreateDefaultColumns() =>
    [
        new BoardColumn(ColumnId.Backlog, "Backlog"),
        new BoardColumn(ColumnId.InProgress, "In Progress"),
        new BoardColumn(ColumnId.Review, "Review"),
        new BoardColumn(ColumnId.Done, "Done")
    ];
}
