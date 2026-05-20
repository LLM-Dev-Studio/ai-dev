namespace AiDev.Features.Board;

/// <summary>
/// Represents a project board containing columns, tasks, and pending domain events.
/// </summary>
public sealed class Board
{
    private static readonly DomainError UnknownColumnError = new("BOARD_UNKNOWN_COLUMN", "Column not found.");
    private static readonly DomainError TaskNotFoundError = new("BOARD_TASK_NOT_FOUND", "Task not found.");
    private static readonly DomainError DuplicateTaskError = new("BOARD_DUPLICATE_TASK", "Task already exists on the board.");
    private static readonly DomainError InvalidAssigneeError = new("BOARD_INVALID_ASSIGNEE", "Assignee must be a valid agent slug.");
    private static readonly DomainError OrphanedTaskError = new("BOARD_ORPHANED_TASK", "Task is not assigned to a board column.");
    private static readonly DomainError DuplicateColumnError = new("BOARD_DUPLICATE_COLUMN", "A column with that id already exists.");
    private static readonly DomainError ProtectedColumnError = new("BOARD_PROTECTED_COLUMN", "Backlog and Done columns cannot be removed or renamed.");
    private static readonly DomainError ColumnNotEmptyError = new("BOARD_COLUMN_NOT_EMPTY", "Column must be empty before it can be removed.");

    private readonly List<BoardColumn> _columns;
    private readonly Dictionary<TaskId, BoardTask> _tasks;
    [JsonIgnore] private readonly List<DomainEvent> _domainEvents = [];

    /// <summary>
    /// Initializes a board for the specified project.
    /// </summary>
    /// <param name="projectSlug">The project slug that owns the board.</param>
    /// <param name="columns">The existing board columns, or <see langword="null"/> to create defaults.</param>
    /// <param name="tasks">The existing board tasks, or <see langword="null"/> to start empty.</param>
    public Board(ProjectSlug projectSlug, List<BoardColumn>? columns = null, Dictionary<TaskId, BoardTask>? tasks = null)
    {
        ArgumentNullException.ThrowIfNull(projectSlug);
        ProjectSlug = projectSlug;
        _columns = columns is { Count: > 0 } ? columns : CreateDefaultColumns();
        _tasks = tasks ?? new();
    }

    /// <summary>
    /// Gets the project slug that owns the board.
    /// </summary>
    [JsonIgnore] public ProjectSlug ProjectSlug { get; }

    /// <summary>
    /// Gets the board columns in display order.
    /// </summary>
    public IReadOnlyList<BoardColumn> Columns => _columns.AsReadOnly();

    /// <summary>
    /// Gets the tasks keyed by task identifier.
    /// </summary>
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
        DateTime movedAt,
        List<string>? tags = null)
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
        task.UpdateDetails(title, priority, description, assignee, tags);

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
    /// Clears all tasks in the requested column and removes them from the board.
    /// </summary>
    public Result<int> ClearColumn(ColumnId columnId)
    {
        ArgumentNullException.ThrowIfNull(columnId);

        var column = _columns.FirstOrDefault(c => c.Id == columnId);
        if (column == null)
            return new Err<int>(UnknownColumnError);

        var taskIds = column.TaskIds.ToArray();
        foreach (var taskId in taskIds)
        {
            _tasks.Remove(taskId);
            column.RemoveTask(taskId);
        }

        return new Ok<int>(taskIds.Length);
    }

    /// <summary>
    /// Adds a new column at the end of the board (before the Done column).
    /// </summary>
    public Result<BoardColumn> AddColumn(ColumnId columnId, string title)
    {
        ArgumentNullException.ThrowIfNull(columnId);
        if (string.IsNullOrWhiteSpace(title))
            return new Err<BoardColumn>(new DomainError("BOARD_INVALID_COLUMN_TITLE", "Column title is required."));
        if (_columns.Any(c => c.Id == columnId))
            return new Err<BoardColumn>(DuplicateColumnError);

        var column = new BoardColumn(columnId, title.Trim());

        // Insert before Done if it exists, otherwise append.
        var doneIndex = _columns.FindIndex(c => c.Id == ColumnId.Done);
        if (doneIndex >= 0)
            _columns.Insert(doneIndex, column);
        else
            _columns.Add(column);

        return new Ok<BoardColumn>(column);
    }

    /// <summary>
    /// Renames an existing column. Backlog and Done are protected.
    /// </summary>
    public Result<Unit> RenameColumn(ColumnId columnId, string newTitle)
    {
        ArgumentNullException.ThrowIfNull(columnId);
        if (string.IsNullOrWhiteSpace(newTitle))
            return new Err<Unit>(new DomainError("BOARD_INVALID_COLUMN_TITLE", "Column title is required."));
        if (columnId == ColumnId.Backlog || columnId == ColumnId.Done)
            return new Err<Unit>(ProtectedColumnError);

        var column = _columns.FirstOrDefault(c => c.Id == columnId);
        if (column == null)
            return new Err<Unit>(UnknownColumnError);

        column.Rename(newTitle.Trim());
        return new Ok<Unit>(Unit.Value);
    }

    /// <summary>
    /// Removes a column. Backlog and Done are protected; column must be empty.
    /// </summary>
    public Result<Unit> RemoveColumn(ColumnId columnId)
    {
        ArgumentNullException.ThrowIfNull(columnId);
        if (columnId == ColumnId.Backlog || columnId == ColumnId.Done)
            return new Err<Unit>(ProtectedColumnError);

        var column = _columns.FirstOrDefault(c => c.Id == columnId);
        if (column == null)
            return new Err<Unit>(UnknownColumnError);
        if (column.TaskIds.Count > 0)
            return new Err<Unit>(ColumnNotEmptyError);

        _columns.Remove(column);
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
