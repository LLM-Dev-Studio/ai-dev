namespace AiDev.Features.Board;

/// <summary>
/// Represents a task tracked on a project board.
/// </summary>
public sealed class BoardTask
{
    /// <summary>
    /// Creates a task with normalized optional values and validated required state.
    /// </summary>
    public BoardTask(
        TaskId id,
        string title,
        Priority? priority = null,
        string? description = null,
        string? assignee = null,
        List<string>? tags = null,
        DateTime? createdAt = null,
        DateTime? completedAt = null,
        DateTime? movedAt = null,
        DateTime? nudgedAt = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Task title is required.", nameof(title));

        Id = id;
        Title = title.Trim();
        Priority = NormalizePriority(priority);
        Description = NormalizeOptional(description);
        Assignee = NormalizeOptional(assignee);
        _tags = NormalizeTags(tags);
        CreatedAt = createdAt;
        CompletedAt = completedAt;
        MovedAt = movedAt;
        NudgedAt = nudgedAt;
    }

    private List<string> _tags;

    /// <summary>
    /// Gets the unique identifier of the task.
    /// </summary>
    public TaskId Id { get; }

    /// <summary>
    /// Gets the current task title.
    /// </summary>
    public string Title { get; private set; }

    /// <summary>
    /// Gets the current task priority.
    /// </summary>
    public Priority Priority { get; private set; }

    /// <summary>
    /// Gets the optional task description.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Gets the optional assigned agent slug.
    /// </summary>
    public string? Assignee { get; private set; }

    /// <summary>
    /// Gets the normalized task tags.
    /// </summary>
    public IReadOnlyList<string> Tags => _tags;

    /// <summary>
    /// Gets the timestamp when the task was created.
    /// </summary>
    public DateTime? CreatedAt { get; private set; }

    /// <summary>
    /// Gets the timestamp when the task was completed.
    /// </summary>
    public DateTime? CompletedAt { get; private set; }
    /// <summary>Timestamp when the task last moved to its current column. Used by overwatch for stall detection.</summary>
    public DateTime? MovedAt { get; private set; }
    /// <summary>Timestamp of the last overwatch nudge. Used to enforce nudge cooldown.</summary>
    public DateTime? NudgedAt { get; private set; }

    /// <summary>
    /// Updates editable task details while keeping optional values normalized.
    /// </summary>
    public void UpdateDetails(string title, Priority? priority, string? description, string? assignee, List<string>? tags = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Task title is required.", nameof(title));

        Title = title.Trim();
        Priority = NormalizePriority(priority);
        Description = NormalizeOptional(description);
        Assignee = NormalizeOptional(assignee);
        _tags = NormalizeTags(tags);
    }

    /// <summary>
    /// Merges additional tags onto the task, ignoring duplicates.
    /// </summary>
    public void MergeTags(IEnumerable<string> newTags)
    {
        foreach (var tag in newTags)
        {
            var normalized = tag?.Trim();
            if (!string.IsNullOrEmpty(normalized) && !_tags.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                _tags.Add(normalized);
        }
    }

    /// <summary>
    /// Records that the task moved to a column and updates completion state accordingly.
    /// </summary>
    public void MoveToColumn(ColumnId columnId, DateTime movedAt)
    {
        ArgumentNullException.ThrowIfNull(columnId);

        MovedAt = movedAt;
        NudgedAt = null;
        CompletedAt = columnId == ColumnId.Done ? movedAt : null;
    }

    /// <summary>
    /// Records when overwatch last nudged the task.
    /// </summary>
    public void MarkNudged(DateTime nudgedAt) => NudgedAt = nudgedAt;

    private static Priority NormalizePriority(Priority? priority)
        => priority ?? Priority.Normal;

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static List<string> NormalizeTags(List<string>? tags)
    {
        if (tags == null || tags.Count == 0) return [];
        return [.. tags.Select(t => t?.Trim() ?? string.Empty)
                   .Where(t => t.Length > 0)
                   .Distinct(StringComparer.OrdinalIgnoreCase)];
    }
}
