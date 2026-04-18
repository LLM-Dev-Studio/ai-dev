namespace AiDev.Features.Board;

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
        Tags = NormalizeTags(tags);
        CreatedAt = createdAt;
        CompletedAt = completedAt;
        MovedAt = movedAt;
        NudgedAt = nudgedAt;
    }

    public TaskId Id { get; }
    public string Title { get; private set; }
    public Priority Priority { get; private set; }
    public string? Description { get; private set; }
    public string? Assignee { get; private set; }
    public List<string> Tags { get; private set; }
    public DateTime? CreatedAt { get; private set; }
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
        Tags = NormalizeTags(tags);
    }

    /// <summary>
    /// Merges additional tags onto the task, ignoring duplicates.
    /// </summary>
    public void MergeTags(IEnumerable<string> newTags)
    {
        foreach (var tag in newTags)
        {
            var normalized = tag?.Trim();
            if (!string.IsNullOrEmpty(normalized) && !Tags.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                Tags.Add(normalized);
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
        return tags.Select(t => t?.Trim() ?? string.Empty)
                   .Where(t => t.Length > 0)
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .ToList();
    }
}
