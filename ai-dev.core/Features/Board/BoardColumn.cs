namespace AiDev.Features.Board;

/// <summary>
/// Represents a column on a board and the tasks assigned to it.
/// </summary>
public sealed class BoardColumn
{
    private readonly List<TaskId> _taskIds;

    /// <summary>
    /// Creates a board column with a validated identity and optional existing task ids.
    /// </summary>
    public BoardColumn(ColumnId id, string title, List<TaskId>? taskIds = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Column title is required.", nameof(title));

        Id = id;
        Title = title;
        _taskIds = taskIds ?? [];
    }

    /// <summary>
    /// Gets the unique identifier for the column.
    /// </summary>
    public ColumnId Id { get; }

    /// <summary>
    /// Gets the display title of the column.
    /// </summary>
    public string Title { get; private set; }

    /// <summary>
    /// Gets the task identifiers currently assigned to the column.
    /// </summary>
    public IReadOnlyList<TaskId> TaskIds => _taskIds.AsReadOnly();

    /// <summary>
    /// Updates the display title of the column.
    /// </summary>
    public void Rename(string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
            throw new ArgumentException("Column title is required.", nameof(newTitle));
        Title = newTitle;
    }

    /// <summary>
    /// Adds a task id to the column if it is not already present.
    /// </summary>
    public void AddTask(TaskId taskId)
    {
        ArgumentNullException.ThrowIfNull(taskId);

        if (!_taskIds.Contains(taskId))
            _taskIds.Add(taskId);
    }

    /// <summary>
    /// Removes a task id from the column when present.
    /// </summary>
    public bool RemoveTask(TaskId taskId)
    {
        ArgumentNullException.ThrowIfNull(taskId);
        return _taskIds.Remove(taskId);
    }

    /// <summary>
    /// Indicates whether the column currently contains the given task id.
    /// </summary>
    public bool ContainsTask(TaskId taskId)
    {
        ArgumentNullException.ThrowIfNull(taskId);
        return _taskIds.Contains(taskId);
    }
}
