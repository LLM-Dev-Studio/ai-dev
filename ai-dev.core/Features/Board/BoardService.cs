namespace AiDev.Features.Board
{
    internal sealed class BoardState
    {
        public List<BoardColumnState>? Columns { get; init; }
        public Dictionary<string, BoardTaskState>? Tasks { get; init; }
    }

    internal sealed class BoardColumnState
    {
        public string? Id { get; init; }
        public string? Title { get; init; }
        public List<string>? TaskIds { get; init; }
    }

    internal sealed class BoardTaskState
    {
        public string? Id { get; init; }
        public string? Title { get; init; }
        public string? Priority { get; init; }
        public string? Description { get; init; }
        public string? Assignee { get; init; }
        public List<string>? Tags { get; init; }
        public DateTime? CreatedAt { get; init; }
        public DateTime? CompletedAt { get; init; }
        public DateTime? MovedAt { get; init; }
        public DateTime? NudgedAt { get; init; }
    }

    public class BoardService(
        WorkspacePaths paths,
        IDomainEventDispatcher dispatcher,
        AtomicFileWriter fileWriter,
        ProjectMutationCoordinator coordinator,
        ILogger<BoardService> logger,
        ProjectStateChangedNotifier projectStateChangedNotifier)
    {
        private static readonly DomainError InvalidColumnError = new("BOARD_INVALID_COLUMN", "Column id is invalid.");
        private static readonly TimeSpan DispatchTimeout = TimeSpan.FromSeconds(10);

        public Board LoadBoard(ProjectSlug projectSlug)
        {
            var path = paths.BoardPath(projectSlug);
            if (!File.Exists(path)) return new(projectSlug);
            try
            {
                var json = File.ReadAllText(path);
                var state = JsonSerializer.Deserialize<BoardState>(json, JsonDefaults.Read);
                var columns = DeserializeColumns(state?.Columns);
                var tasks = DeserializeTasks(state?.Tasks);
                return new Board(projectSlug, columns, tasks);
            }
            catch (Exception ex)
            {
                var backupPath = path + $".corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}";
                try { File.Move(path, backupPath); } catch { /* best-effort */ }
                logger.LogError(ex, "[board] Corrupt board.json for {ProjectSlug}; backed up to {Backup} and reset to default",
                    projectSlug, backupPath);
                return new Board(projectSlug);
            }
        }

        public void SaveBoard(ProjectSlug projectSlug, Board board)
        {
            var path = paths.BoardPath(projectSlug);
            var state = new BoardState
            {
                Columns = board.Columns.Select(column => new BoardColumnState
                {
                    Id = column.Id.Value,
                    Title = column.Title,
                    TaskIds = [.. column.TaskIds.Select(taskId => taskId.Value)],
                }).ToList(),
                Tasks = board.Tasks.ToDictionary(
                    kv => kv.Key.Value,
                    kv => new BoardTaskState
                    {
                        Id = kv.Value.Id.Value,
                        Title = kv.Value.Title,
                        Priority = kv.Value.Priority.Value,
                        Description = kv.Value.Description,
                        Assignee = kv.Value.Assignee,
                        Tags = kv.Value.Tags.Count > 0 ? [.. kv.Value.Tags] : null,
                        CreatedAt = kv.Value.CreatedAt,
                        CompletedAt = kv.Value.CompletedAt,
                        MovedAt = kv.Value.MovedAt,
                        NudgedAt = kv.Value.NudgedAt,
                    }),
            };
            fileWriter.WriteAllText(path, JsonSerializer.Serialize(state, JsonDefaults.WriteIgnoreNull));
            projectStateChangedNotifier.Notify(projectSlug, ProjectStateChangeKind.Board);
        }

        private static List<BoardColumn>? DeserializeColumns(List<BoardColumnState>? columns)
        {
            if (columns == null)
                return null;

            var result = new List<BoardColumn>(columns.Count);
            foreach (var column in columns)
            {
                if (!ColumnId.TryParse(column.Id, out var columnId) || string.IsNullOrWhiteSpace(column.Title))
                    continue;

                var taskIds = (column.TaskIds ?? [])
                    .Where(static id => TaskId.TryParse(id, out _))
                    .Select(id => new TaskId(id))
                    .ToList();

                result.Add(new BoardColumn(columnId, column.Title, taskIds));
            }

            return result;
        }

        private static Dictionary<TaskId, BoardTask>? DeserializeTasks(Dictionary<string, BoardTaskState>? tasksState)
        {
            if (tasksState == null)
                return null;

            var tasks = new Dictionary<TaskId, BoardTask>();
            foreach (var (key, taskState) in tasksState)
            {
                if (!TaskId.TryParse(key, out var taskId) || taskState == null || string.IsNullOrWhiteSpace(taskState.Title))
                    continue;

                tasks[taskId] = new BoardTask(
                    id: taskId,
                    title: taskState.Title,
                    priority: string.IsNullOrWhiteSpace(taskState.Priority) ? null : Priority.From(taskState.Priority),
                    description: taskState.Description,
                    assignee: taskState.Assignee,
                    tags: taskState.Tags,
                    createdAt: taskState.CreatedAt,
                    completedAt: taskState.CompletedAt,
                    movedAt: taskState.MovedAt,
                    nudgedAt: taskState.NudgedAt);
            }

            return tasks;
        }

        public async Task<Result<BoardTask>> CreateTaskAsync(ProjectSlug projectSlug, string columnId, string title,
            string? description, string priority, string? assignee, List<string>? tags = null, CancellationToken cancellationToken = default)
            => await coordinator.ExecuteAsync(projectSlug, async () =>
            {
                using var activity = AiDevTelemetry.ActivitySource.StartActivity("Board.CreateTask", ActivityKind.Internal);
                activity?.SetTag("project.slug", projectSlug.Value);
                activity?.SetTag("board.column", columnId);
                activity?.SetTag("task.assignee", assignee ?? string.Empty);
                cancellationToken.ThrowIfCancellationRequested();
                if (!ColumnId.TryParse(columnId, out var parsedColumnId))
                    return new Err<BoardTask>(InvalidColumnError);

                var board = LoadBoard(projectSlug);
                var result = CreateBoardTask(title, priority, description, assignee, tags)
                    .Then(task => board.AddTask(parsedColumnId, task));

                return await result.Match<BoardTask, Task<Result<BoardTask>>>(
                    task => PersistBoardResultAsync(projectSlug, board, task),
                    error => Task.FromResult<Result<BoardTask>>(new Err<BoardTask>(error))).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

        public async Task<Result<BoardTask>> UpdateTaskAsync(ProjectSlug projectSlug, TaskId taskId, string newColumnId,
            string title, string? description, string priority, string? assignee, List<string>? tags = null, CancellationToken cancellationToken = default)
            => await coordinator.ExecuteAsync(projectSlug, async () =>
            {
                using var activity = AiDevTelemetry.ActivitySource.StartActivity("Board.UpdateTask", ActivityKind.Internal);
                activity?.SetTag("project.slug", projectSlug.Value);
                activity?.SetTag("task.id", taskId.Value);
                activity?.SetTag("board.column", newColumnId);
                cancellationToken.ThrowIfCancellationRequested();
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
                    DateTime.UtcNow,
                    tags);

                return await result.Match<BoardTask, Task<Result<BoardTask>>>(
                    task => PersistBoardResultAsync(projectSlug, board, task),
                    error => Task.FromResult<Result<BoardTask>>(new Err<BoardTask>(error))).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

        public void SetTaskNudged(ProjectSlug projectSlug, TaskId taskId)
            => coordinator.Execute(projectSlug, () =>
            {
                var board = LoadBoard(projectSlug);
                var result = board.MarkTaskNudged(taskId, DateTime.UtcNow);
                if (result is Ok<Unit>)
                    SaveBoard(projectSlug, board);

                return Unit.Value;
            });

        public Task<Result<Unit>> DeleteTaskAsync(ProjectSlug projectSlug, TaskId taskId, CancellationToken cancellationToken = default)
            => coordinator.ExecuteAsync(projectSlug, () =>
            {
                using var activity = AiDevTelemetry.ActivitySource.StartActivity("Board.DeleteTask", ActivityKind.Internal);
                activity?.SetTag("project.slug", projectSlug.Value);
                activity?.SetTag("task.id", taskId.Value);
                cancellationToken.ThrowIfCancellationRequested();
                var board = LoadBoard(projectSlug);
                var result = board.DeleteTask(taskId);

                return result.Match<Unit, Result<Unit>>(
                    _ =>
                    {
                        SaveBoard(projectSlug, board);
                        return new Ok<Unit>(Unit.Value);
                    },
                    error => new Err<Unit>(error));
            }, cancellationToken);

        public Task<Result<int>> ClearColumnAsync(ProjectSlug projectSlug, ColumnId columnId, CancellationToken cancellationToken = default)
            => coordinator.ExecuteAsync(projectSlug, () =>
            {
                using var activity = AiDevTelemetry.ActivitySource.StartActivity("Board.ClearColumn", ActivityKind.Internal);
                activity?.SetTag("project.slug", projectSlug.Value);
                activity?.SetTag("board.column", columnId.Value);
                cancellationToken.ThrowIfCancellationRequested();
                var board = LoadBoard(projectSlug);
                var result = board.ClearColumn(columnId);

                return result.Match<int, Result<int>>(
                    clearedCount =>
                    {
                        SaveBoard(projectSlug, board);
                        return new Ok<int>(clearedCount);
                    },
                    error => new Err<int>(error));
            }, cancellationToken);

        private async Task<Result<BoardTask>> PersistBoardResultAsync(ProjectSlug projectSlug, Board board, BoardTask task)
        {
            SaveBoard(projectSlug, board);
            var dispatchResult = await DispatchBoardEventsAsync(board.DequeueDomainEvents()).ConfigureAwait(false);
            if (dispatchResult is Err<Unit> err)
                return new Err<BoardTask>(err.Error);

            return new Ok<BoardTask>(task);
        }

        private Result<BoardTask> CreateBoardTask(string title, string priority, string? description, string? assignee, List<string>? tags = null)
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
                    tags: tags,
                    createdAt: now,
                    movedAt: now));
            }
            catch (ArgumentException ex)
            {
                return new Err<BoardTask>(new DomainError("BOARD_INVALID_TASK", ex.Message));
            }
        }

        /// <summary>
        /// Returns the allowed tag strings for a project from allowed-tags.json, or null if no allowlist is configured.
        /// </summary>
        public List<string>? GetAllowedTags(ProjectSlug projectSlug)
        {
            var path = Path.Combine(paths.ProjectDir(projectSlug).Value, "allowed-tags.json");
            if (!File.Exists(path)) return null;
            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<string>>(json, JsonDefaults.Read);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[board] Failed to read allowed-tags.json for {ProjectSlug}", projectSlug.Value);
                return null;
            }
        }

        /// <summary>
        /// Merges tags from a result payload onto the specified task and persists.
        /// No-ops silently if the task does not exist or tags is null/empty.
        /// </summary>
        public void MergeTaskTagsFromResult(ProjectSlug projectSlug, TaskId taskId, IEnumerable<string>? tags)
        {
            if (tags == null) return;
            var tagList = tags.ToList();
            if (tagList.Count == 0) return;

            coordinator.Execute(projectSlug, () =>
            {
                var board = LoadBoard(projectSlug);
                if (!board.Tasks.TryGetValue(taskId, out var task)) return Unit.Value;
                task.MergeTags(tagList);
                SaveBoard(projectSlug, board);
                return Unit.Value;
            });
        }

        /// <summary>
        /// Automatically moves a board task to Done and merges any tags from the session result.
        /// No-ops silently if the task does not exist or is already in Done.
        /// </summary>
        public void CompleteTaskFromResult(ProjectSlug projectSlug, TaskId taskId, SessionResult result)
        {
            coordinator.Execute(projectSlug, () =>
            {
                var board = LoadBoard(projectSlug);
                if (!board.Tasks.TryGetValue(taskId, out var task))
                {
                    logger.LogWarning("[board] CompleteTaskFromResult: task {TaskId} not found in {ProjectSlug} — result.json may reference a stale or mistyped task ID",
                        taskId.Value, projectSlug.Value);
                    return Unit.Value;
                }

                // Skip if already in Done.
                var currentColumn = board.Columns.FirstOrDefault(c => c.ContainsTask(task.Id));
                if (currentColumn?.Id == ColumnId.Done) return Unit.Value;

                // Merge tags from result before moving.
                if (result.Tags is { Count: > 0 })
                    task.MergeTags(result.Tags);

                var completedAt = result.CompletedAt ?? DateTime.UtcNow;
                board.UpdateTask(
                    taskId,
                    ColumnId.Done,
                    task.Title,
                    task.Priority,
                    task.Description,
                    task.Assignee,
                    completedAt,
                    task.Tags.Count > 0 ? [.. task.Tags] : null);

                SaveBoard(projectSlug, board);
                return Unit.Value;
            });
        }

        private async Task<Result<Unit>> DispatchBoardEventsAsync(IReadOnlyList<DomainEvent> domainEvents)
        {
            if (domainEvents.Count == 0)
                return new Ok<Unit>(Unit.Value);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
            timeoutCts.CancelAfter(DispatchTimeout);
            var dispatchResult = await dispatcher.Dispatch(domainEvents, timeoutCts.Token).ConfigureAwait(false);
            return dispatchResult;
        }
    }
}
