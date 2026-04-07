using AiDev.Features.Agent;

namespace AiDev.Features.Board;

public class BoardService(WorkspacePaths paths, AgentRunnerService agentRunner, ILogger<BoardService> logger)
{
    private static readonly DomainError InvalidColumnError = new("BOARD_INVALID_COLUMN", "Column id is invalid.");

    public Board LoadBoard(ProjectSlug projectSlug)
    {
        var path = paths.BoardPath(projectSlug);
        if (!File.Exists(path)) return new();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Board>(json, JsonDefaults.WriteIgnoreNull) ?? new Board();
        }
        catch { return new(); }
    }

    public void SaveBoard(ProjectSlug projectSlug, Board board)
    {
        var path = paths.BoardPath(projectSlug);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(board, JsonDefaults.WriteIgnoreNull));
    }

    public Result<BoardTask> CreateTask(ProjectSlug projectSlug, string columnId, string title,
        string? description, string priority, string? assignee)
    {
        if (!ColumnId.TryParse(columnId, out var parsedColumnId))
            return new Err<BoardTask>(InvalidColumnError);

        var board = LoadBoard(projectSlug);
        var result = CreateBoardTask(title, priority, description, assignee)
            .Then(task => board.AddTask(parsedColumnId, task));

        return result.Match(
            task => PersistBoardResult(projectSlug, board, task),
            error => new Err<BoardTask>(error));
    }

    public Result<BoardTask> UpdateTask(ProjectSlug projectSlug, TaskId taskId, string newColumnId,
        string title, string? description, string priority, string? assignee)
    {
        if (!ColumnId.TryParse(newColumnId, out var parsedColumnId))
            return new Err<BoardTask>(InvalidColumnError);

        var board = LoadBoard(projectSlug);
        var result = board.UpdateTask(
            taskId,
            parsedColumnId,
            title,
            Priority.From(priority),
            description,
            assignee,
            DateTime.UtcNow);

        return result.Match(
            task => PersistBoardResult(projectSlug, board, task),
            error => new Err<BoardTask>(error));
    }

    public void SetTaskNudged(ProjectSlug projectSlug, TaskId taskId)
    {
        var board = LoadBoard(projectSlug);
        var result = board.MarkTaskNudged(taskId, DateTime.UtcNow);
        if (result is Ok<Unit>)
            SaveBoard(projectSlug, board);
    }

    public Result<Unit> DeleteTask(ProjectSlug projectSlug, TaskId taskId)
    {
        var board = LoadBoard(projectSlug);
        var result = board.DeleteTask(taskId);

        return result.Match<Unit, Result<Unit>>(
            (Unit _) =>
            {
                SaveBoard(projectSlug, board);
                return new Ok<Unit>(Unit.Value);
            },
            error => new Err<Unit>(error));
    }

    private Result<BoardTask> PersistBoardResult(ProjectSlug projectSlug, Board board, BoardTask task)
    {
        SaveBoard(projectSlug, board);
        DispatchBoardEvents(projectSlug, board.DequeueDomainEvents());
        return new Ok<BoardTask>(task);
    }

    private Result<BoardTask> CreateBoardTask(string title, string priority, string? description, string? assignee)
    {
        try
        {
            var now = DateTime.UtcNow;
            return new Ok<BoardTask>(new BoardTask(
                id: TaskId.New(),
                title: title,
                priority: Priority.From(priority),
                description: description,
                assignee: assignee,
                createdAt: now,
                movedAt: now));
        }
        catch (ArgumentException ex)
        {
            return new Err<BoardTask>(new DomainError("BOARD_INVALID_TASK", ex.Message));
        }
    }

    private void DispatchBoardEvents(ProjectSlug projectSlug, IReadOnlyList<DomainEvent> domainEvents)
    {
        foreach (var domainEvent in domainEvents)
        {
            if (domainEvent is not TaskAssigned taskAssigned)
                continue;

            var err = agentRunner.WriteInboxMessage(
                projectSlug,
                taskAssigned.Assignee,
                from: "board",
                re: taskAssigned.Title,
                type: "task-assigned",
                priority: taskAssigned.Priority.Value,
                body: $"You have been assigned a new task: {taskAssigned.Title}{(string.IsNullOrWhiteSpace(taskAssigned.Description) ? "" : $"\n\n{taskAssigned.Description}")}",
                taskId: taskAssigned.TaskId);

            if (err != null)
                logger.LogError("[board] Failed to dispatch TaskAssigned to {Assignee} for task {TaskId}: {Error}",
                    taskAssigned.Assignee, taskAssigned.TaskId, err);
            else
                logger.LogInformation("[board] Dispatched TaskAssigned to {Assignee} for task {TaskId} ({Title})",
                    taskAssigned.Assignee, taskAssigned.TaskId, taskAssigned.Title);
        }
    }
}
