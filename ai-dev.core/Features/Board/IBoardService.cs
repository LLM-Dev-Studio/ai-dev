namespace AiDev.Features.Board;

/// <summary>
/// Provides persistence and mutation operations for project boards.
/// </summary>
public interface IBoardService
{
    /// <summary>
    /// Loads the board for a project.
    /// </summary>
    /// <param name="projectSlug">The project whose board should be loaded.</param>
    /// <returns>The loaded board, or a default empty board when none exists.</returns>
    Board LoadBoard(ProjectSlug projectSlug);

    /// <summary>
    /// Saves the board for a project.
    /// </summary>
    /// <param name="projectSlug">The project whose board should be saved.</param>
    /// <param name="board">The board to save.</param>
    void SaveBoard(ProjectSlug projectSlug, Board board);

    /// <summary>
    /// Creates a new task on the board.
    /// </summary>
    Task<Result<BoardTask>> CreateTaskAsync(ProjectSlug projectSlug, string columnId, string title,
        string? description, string priority, string? assignee, List<string>? tags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing task on the board.
    /// </summary>
    Task<Result<BoardTask>> UpdateTaskAsync(ProjectSlug projectSlug, TaskId taskId, string newColumnId,
        string title, string? description, string priority, string? assignee, List<string>? tags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a task as nudged.
    /// </summary>
    void SetTaskNudged(ProjectSlug projectSlug, TaskId taskId);

    /// <summary>
    /// Deletes a task from the board.
    /// </summary>
    Task<Result<Unit>> DeleteTaskAsync(ProjectSlug projectSlug, TaskId taskId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all tasks from a column.
    /// </summary>
    Task<Result<int>> ClearColumnAsync(ProjectSlug projectSlug, ColumnId columnId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the allowed tag strings for a project from allowed-tags.json, or null if no allowlist is configured.
    /// </summary>
    List<string>? GetAllowedTags(ProjectSlug projectSlug);

    /// <summary>
    /// Merges tags from a result payload onto the specified task and persists.
    /// No-ops silently if the task does not exist or tags is null/empty.
    /// </summary>
    void MergeTaskTagsFromResult(ProjectSlug projectSlug, TaskId taskId, IEnumerable<string>? tags);

    /// <summary>
    /// Automatically moves a board task to Done and merges any tags from the session result.
    /// No-ops silently if the task does not exist or is already in Done.
    /// </summary>
    void CompleteTaskFromResult(ProjectSlug projectSlug, TaskId taskId, SessionResult result);
}
