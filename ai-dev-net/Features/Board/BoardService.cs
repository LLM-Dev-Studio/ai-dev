using AiDevNet.Features.Agent;

namespace AiDevNet.Features.Board;

public class BoardService(WorkspacePaths paths, AgentRunnerService agentRunner, ILogger<BoardService> logger)
{

    public BoardData LoadBoard(ProjectSlug projectSlug)
    {
        var path = paths.BoardPath(projectSlug);
        if (!File.Exists(path)) return new();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<BoardData>(json, JsonDefaults.WriteIgnoreNull) ?? new BoardData();
        }
        catch { return new(); }
    }

    public void SaveBoard(ProjectSlug projectSlug, BoardData board)
    {
        var path = paths.BoardPath(projectSlug);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(board, JsonDefaults.WriteIgnoreNull));
    }

    public string? CreateTask(ProjectSlug projectSlug, string columnId, string title,
        string? description, string priority, string? assignee)
    {
        var board = LoadBoard(projectSlug);
        var col = board.Columns.FirstOrDefault(c => c.Id == columnId);
        if (col == null) return "Column not found.";

        var id = TaskId.New();
        var now = DateTime.UtcNow;
        board.Tasks[id] = new()
        {
            Id = id,
            Title = title,
            Priority = priority,
            Description = string.IsNullOrWhiteSpace(description) ? null : description,
            Assignee = string.IsNullOrWhiteSpace(assignee) ? null : assignee,
            CreatedAt = now,
            MovedAt = now,
        };
        col.TaskIds.Add(id);
        SaveBoard(projectSlug, board);

        // Send message to assignee if set
        if (!string.IsNullOrWhiteSpace(assignee))
        {
            var err = agentRunner.WriteInboxMessage(
                projectSlug,
                assignee,
                from: "board",
                re: title,
                type: "task-assigned",
                priority: priority,
                body: $"You have been assigned a new task: {title}{(string.IsNullOrWhiteSpace(description) ? "" : $"\n\n{description}")}",
                taskId: id);
            if (err != null)
                logger.LogError("[board] Failed to send task-assigned inbox message to {Assignee} for task {TaskId}: {Error}",
                    assignee, id, err);
            else
                logger.LogInformation("[board] Sent task-assigned message to {Assignee} for task {TaskId} ({Title})",
                    assignee, id, title);
        }
        return null;
    }

    public string? UpdateTask(ProjectSlug projectSlug, TaskId taskId, string newColumnId,
        string title, string? description, string priority, string? assignee)
    {
        var board = LoadBoard(projectSlug);
        if (!board.Tasks.TryGetValue(taskId, out var task)) return "Task not found.";


        var previousAssignee = task.Assignee;
        task.Title = title;
        task.Priority = priority;
        task.Description = string.IsNullOrWhiteSpace(description) ? null : description;
        task.Assignee = string.IsNullOrWhiteSpace(assignee) ? null : assignee;

        // Move between columns if needed
        var currentCol = board.Columns.FirstOrDefault(c => c.TaskIds.Contains(taskId));
        if (currentCol?.Id != newColumnId)
        {
            currentCol?.TaskIds.Remove(taskId);
            var newCol = board.Columns.FirstOrDefault(c => c.Id == newColumnId);
            newCol?.TaskIds.Add(taskId);

            var movedNow = DateTime.UtcNow;
            task.MovedAt = movedNow;
            task.NudgedAt = null; // reset nudge on column move — task is progressing

            if (newColumnId == "done" && task.CompletedAt == null)
                task.CompletedAt = movedNow;
            else if (newColumnId != "done")
                task.CompletedAt = null;
        }

        SaveBoard(projectSlug, board);

        // Only send message if assignee is set and changed
        if (string.IsNullOrWhiteSpace(assignee) || assignee == previousAssignee)
        {
            return null;
        }

        var err = agentRunner.WriteInboxMessage(
            projectSlug,
            assignee,
            from: "board",
            re: title,
            type: "task-assigned",
            priority: priority,
            body: $"You have been assigned a new task: {title}{(string.IsNullOrWhiteSpace(description) ? "" : $"\n\n{description}")}",
            taskId: taskId);
        if (err != null)
            logger.LogError("[board] Failed to send task-assigned inbox message to {Assignee} for task {TaskId}: {Error}",
                assignee, taskId, err);
        else
            logger.LogInformation("[board] Sent task-assigned message to {Assignee} for task {TaskId} ({Title})",
                assignee, taskId, title);
        return null;
    }

    public void SetTaskNudged(ProjectSlug projectSlug, TaskId taskId)
    {
        var board = LoadBoard(projectSlug);
        if (!board.Tasks.TryGetValue(taskId, out var task)) return;
        task.NudgedAt = DateTime.UtcNow;
        SaveBoard(projectSlug, board);
    }

    public string? DeleteTask(ProjectSlug projectSlug, TaskId taskId)
    {
        var board = LoadBoard(projectSlug);
        if (!board.Tasks.Remove(taskId)) return "Task not found.";

        foreach (var col in board.Columns)
            col.TaskIds.Remove(taskId);

        SaveBoard(projectSlug, board);
        return null;
    }
}
