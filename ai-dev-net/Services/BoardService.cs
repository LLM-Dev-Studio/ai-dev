
namespace AiDevNet.Services;

public class BoardData
{
    [JsonPropertyName("columns")] public List<BoardColumn> Columns { get; set; } = [];
    [JsonPropertyName("tasks")] public Dictionary<string, BoardTask> Tasks { get; set; } = new();
}

public class BoardColumn
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("taskIds")] public List<string> TaskIds { get; set; } = [];
}

public class BoardTask
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("priority")] public string Priority { get; set; } = "normal";
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("assignee")] public string? Assignee { get; set; }
    [JsonPropertyName("createdAt")] public string? CreatedAt { get; set; }
    [JsonPropertyName("completedAt")] public string? CompletedAt { get; set; }
    /// <summary>Timestamp when the task last moved to its current column. Used by overwatch for stall detection.</summary>
    [JsonPropertyName("movedAt")] public string? MovedAt { get; set; }
    /// <summary>Timestamp of the last overwatch nudge. Used to enforce nudge cooldown.</summary>
    [JsonPropertyName("nudgedAt")] public string? NudgedAt { get; set; }
}

public class BoardService(WorkspacePaths paths, AgentRunnerService agentRunner, ILogger<BoardService> logger)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public BoardData LoadBoard(ProjectSlug projectSlug)
    {
        var path = paths.BoardPath(projectSlug);
        if (!File.Exists(path)) return new();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<BoardData>(json, Options) ?? new BoardData();
        }
        catch { return new(); }
    }

    public void SaveBoard(ProjectSlug projectSlug, BoardData board)
    {
        var path = paths.BoardPath(projectSlug);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(board, Options));
    }

    public string? CreateTask(ProjectSlug projectSlug, string columnId, string title,
        string? description, string priority, string? assignee)
    {
        var board = LoadBoard(projectSlug);
        var col = board.Columns.FirstOrDefault(c => c.Id == columnId);
        if (col == null) return "Column not found.";

        var id = $"task-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid().ToString("N")[..5]}";
        var now = DateTime.UtcNow.ToString("o");
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

    public string? UpdateTask(ProjectSlug projectSlug, string taskId, string newColumnId,
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

            var movedNow = DateTime.UtcNow.ToString("o");
            task.MovedAt = movedNow;
            task.NudgedAt = null; // reset nudge on column move — task is progressing

            if (newColumnId == "done" && task.CompletedAt == null)
                task.CompletedAt = movedNow;
            else if (newColumnId != "done")
                task.CompletedAt = null;
        }

        SaveBoard(projectSlug, board);

        // Only send message if assignee is set and changed
        if (!string.IsNullOrWhiteSpace(assignee) && assignee != previousAssignee)
        {
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
        }
        return null;
    }

    public void SetTaskNudged(ProjectSlug projectSlug, string taskId)
    {
        var board = LoadBoard(projectSlug);
        if (!board.Tasks.TryGetValue(taskId, out var task)) return;
        task.NudgedAt = DateTime.UtcNow.ToString("o");
        SaveBoard(projectSlug, board);
    }

    public string? DeleteTask(ProjectSlug projectSlug, string taskId)
    {
        var board = LoadBoard(projectSlug);
        if (!board.Tasks.ContainsKey(taskId)) return "Task not found.";

        board.Tasks.Remove(taskId);
        foreach (var col in board.Columns)
            col.TaskIds.Remove(taskId);

        SaveBoard(projectSlug, board);
        return null;
    }
}
