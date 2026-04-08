using AiDev.Features.Board;
using AiDev.Features.Workspace;

namespace AiDev.Services;

/// <summary>
/// Validates persisted project state for low-risk consistency issues and applies safe repairs where possible.
/// </summary>
public class ConsistencyCheckService(
    WorkspacePaths paths,
    WorkspaceService workspaceService,
    BoardService boardService,
    ILogger<ConsistencyCheckService> logger)
{
    private sealed class RawBoardState
    {
        public List<RawBoardColumn>? Columns { get; init; }
        public Dictionary<string, JsonElement>? Tasks { get; init; }
    }

    private sealed class RawBoardColumn
    {
        public string? Id { get; init; }
        public string? Title { get; init; }
        public List<string>? TaskIds { get; init; }
    }

    public async Task<WorkspaceConsistencyReport> CheckWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        using var activity = AiDevTelemetry.ActivitySource.StartActivity("Consistency.CheckWorkspace", ActivityKind.Internal);
        var startedAt = Stopwatch.GetTimestamp();
        AiDevTelemetry.ConsistencyChecksStarted.Add(1);

        var reports = new List<ProjectConsistencyReport>();
        foreach (var project in workspaceService.ListProjects())
        {
            cancellationToken.ThrowIfCancellationRequested();
            reports.Add(await CheckProjectAsync(project.Slug, cancellationToken).ConfigureAwait(false));
        }

        var report = new WorkspaceConsistencyReport(reports);
        activity?.SetTag("consistency.projects", reports.Count);
        activity?.SetTag("consistency.errors", report.ErrorCount);
        activity?.SetTag("consistency.warnings", report.WarningCount);
        AiDevTelemetry.ConsistencyCheckDurationMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        return report;
    }

    public Task<ProjectConsistencyReport> CheckProjectAsync(ProjectSlug projectSlug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectSlug);

        return Task.Run(() =>
        {
            using var activity = AiDevTelemetry.ActivitySource.StartActivity("Consistency.CheckProject", ActivityKind.Internal);
            activity?.SetTag("project.slug", projectSlug.Value);

            var findings = new List<ConsistencyFinding>();
            InspectBoard(projectSlug, findings, cancellationToken);
            InspectDecisions(projectSlug, findings, cancellationToken);

            foreach (var finding in findings)
            {
                AiDevTelemetry.ConsistencyFindings.Add(1,
                    new KeyValuePair<string, object?>("severity", finding.Severity.ToString()),
                    new KeyValuePair<string, object?>("code", finding.Code));
            }

            return new ProjectConsistencyReport(projectSlug, findings);
        }, cancellationToken);
    }

    private void InspectBoard(ProjectSlug projectSlug, List<ConsistencyFinding> findings, CancellationToken cancellationToken)
    {
        var rawState = LoadRawBoardState(projectSlug, findings);
        if (rawState == null)
            return;

        var board = boardService.LoadBoard(projectSlug);
        var repairedColumns = new List<BoardColumn>();
        var seenTaskIds = new HashSet<TaskId>();
        var changed = false;

        InspectRawBoardShape(projectSlug, rawState, findings);

        foreach (var column in board.Columns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var repairedColumn = new BoardColumn(column.Id, column.Title);

            foreach (var taskId in column.TaskIds)
            {
                if (!board.Tasks.ContainsKey(taskId))
                {
                    changed = true;
                    findings.Add(new ConsistencyFinding(
                        "BOARD_COLUMN_ORPHANED_TASK_REFERENCE",
                        ConsistencySeverity.Warning,
                        $"Column '{column.Id.Value}' references missing task '{taskId.Value}'.",
                        ConsistencyFixType.AutoRepaired,
                        projectSlug.Value,
                        taskId.Value));
                    AiDevTelemetry.ConsistencyRepairs.Add(1,
                        new KeyValuePair<string, object?>("code", "BOARD_COLUMN_ORPHANED_TASK_REFERENCE"),
                        new KeyValuePair<string, object?>("project", projectSlug.Value));
                    continue;
                }

                if (!seenTaskIds.Add(taskId))
                {
                    changed = true;
                    findings.Add(new ConsistencyFinding(
                        "BOARD_TASK_DUPLICATE_REFERENCE",
                        ConsistencySeverity.Warning,
                        $"Task '{taskId.Value}' appears in multiple board columns.",
                        ConsistencyFixType.AutoRepaired,
                        projectSlug.Value,
                        taskId.Value));
                    AiDevTelemetry.ConsistencyRepairs.Add(1,
                        new KeyValuePair<string, object?>("code", "BOARD_TASK_DUPLICATE_REFERENCE"),
                        new KeyValuePair<string, object?>("project", projectSlug.Value));
                    continue;
                }

                repairedColumn.AddTask(taskId);
            }

            repairedColumns.Add(repairedColumn);
        }

        foreach (var (columnId, title) in DefaultColumns())
        {
            if (repairedColumns.Any(c => c.Id == columnId))
                continue;

            changed = true;
            repairedColumns.Add(new BoardColumn(columnId, title));
            findings.Add(new ConsistencyFinding(
                "BOARD_COLUMN_MISSING",
                ConsistencySeverity.Warning,
                $"Board is missing required column '{columnId.Value}'.",
                ConsistencyFixType.AutoRepaired,
                projectSlug.Value,
                columnId.Value));
            AiDevTelemetry.ConsistencyRepairs.Add(1,
                new KeyValuePair<string, object?>("code", "BOARD_COLUMN_MISSING"),
                new KeyValuePair<string, object?>("project", projectSlug.Value));
        }

        var backlog = repairedColumns.First(c => c.Id == ColumnId.Backlog);
        foreach (var taskId in board.Tasks.Keys)
        {
            if (repairedColumns.Any(c => c.ContainsTask(taskId)))
                continue;

            cancellationToken.ThrowIfCancellationRequested();
            changed = true;
            backlog.AddTask(taskId);
            findings.Add(new ConsistencyFinding(
                "BOARD_TASK_ORPHANED",
                ConsistencySeverity.Warning,
                $"Task '{taskId.Value}' is not assigned to any board column.",
                ConsistencyFixType.AutoRepaired,
                projectSlug.Value,
                taskId.Value));
            AiDevTelemetry.ConsistencyRepairs.Add(1,
                new KeyValuePair<string, object?>("code", "BOARD_TASK_ORPHANED"),
                new KeyValuePair<string, object?>("project", projectSlug.Value));
        }

        if (!changed)
            return;

        try
        {
            var repairedBoard = new Board(projectSlug, repairedColumns, board.Tasks.ToDictionary(kv => kv.Key, kv => kv.Value));
            boardService.SaveBoard(projectSlug, repairedBoard);
            logger.LogWarning("[consistency] Auto-repaired board state for {Project}", projectSlug);
        }
        catch (IOException ex)
        {
            findings.Add(new ConsistencyFinding(
                "BOARD_REPAIR_FAILED",
                ConsistencySeverity.Error,
                $"Failed to persist board repairs: {ex.Message}",
                ConsistencyFixType.ManualActionRequired,
                projectSlug.Value,
                FilePathConstants.BoardJsonFileName));
        }
    }

    private RawBoardState? LoadRawBoardState(ProjectSlug projectSlug, List<ConsistencyFinding> findings)
    {
        var boardPath = paths.BoardPath(projectSlug);
        if (!boardPath.Exists())
            return new RawBoardState();

        try
        {
            return JsonSerializer.Deserialize<RawBoardState>(File.ReadAllText(boardPath), JsonDefaults.Read);
        }
        catch (JsonException ex)
        {
            findings.Add(new ConsistencyFinding(
                "BOARD_PARSE_FAILED",
                ConsistencySeverity.Error,
                $"Failed to parse raw board state: {ex.Message}",
                ConsistencyFixType.ManualActionRequired,
                projectSlug.Value,
                FilePathConstants.BoardJsonFileName));
            return null;
        }
        catch (IOException ex)
        {
            findings.Add(new ConsistencyFinding(
                "BOARD_READ_FAILED",
                ConsistencySeverity.Error,
                $"Failed to read raw board state: {ex.Message}",
                ConsistencyFixType.ManualActionRequired,
                projectSlug.Value,
                FilePathConstants.BoardJsonFileName));
            return null;
        }
    }

    private static void InspectRawBoardShape(ProjectSlug projectSlug, RawBoardState rawState, List<ConsistencyFinding> findings)
    {
        var rawColumns = rawState.Columns ?? [];
        var rawTasks = rawState.Tasks ?? [];
        var seenTaskIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (columnId, _) in DefaultColumns())
        {
            if (rawColumns.Any(c => string.Equals(c.Id, columnId.Value, StringComparison.OrdinalIgnoreCase)))
                continue;

            findings.Add(new ConsistencyFinding(
                "BOARD_COLUMN_MISSING_RAW",
                ConsistencySeverity.Warning,
                $"Persisted board is missing required column '{columnId.Value}'.",
                ConsistencyFixType.AutoRepaired,
                projectSlug.Value,
                columnId.Value));
            AiDevTelemetry.ConsistencyRepairs.Add(1,
                new KeyValuePair<string, object?>("code", "BOARD_COLUMN_MISSING_RAW"),
                new KeyValuePair<string, object?>("project", projectSlug.Value));
        }

        foreach (var column in rawColumns)
        {
            foreach (var taskId in column.TaskIds ?? [])
            {
                if (!rawTasks.ContainsKey(taskId))
                {
                    findings.Add(new ConsistencyFinding(
                        "BOARD_COLUMN_ORPHANED_TASK_REFERENCE_RAW",
                        ConsistencySeverity.Warning,
                        $"Persisted board column '{column.Id ?? "unknown"}' references missing task '{taskId}'.",
                        ConsistencyFixType.AutoRepaired,
                        projectSlug.Value,
                        taskId));
                    AiDevTelemetry.ConsistencyRepairs.Add(1,
                        new KeyValuePair<string, object?>("code", "BOARD_COLUMN_ORPHANED_TASK_REFERENCE_RAW"),
                        new KeyValuePair<string, object?>("project", projectSlug.Value));
                    continue;
                }

                if (!seenTaskIds.Add(taskId))
                {
                    findings.Add(new ConsistencyFinding(
                        "BOARD_TASK_DUPLICATE_REFERENCE_RAW",
                        ConsistencySeverity.Warning,
                        $"Persisted board task '{taskId}' appears in multiple columns.",
                        ConsistencyFixType.AutoRepaired,
                        projectSlug.Value,
                        taskId));
                    AiDevTelemetry.ConsistencyRepairs.Add(1,
                        new KeyValuePair<string, object?>("code", "BOARD_TASK_DUPLICATE_REFERENCE_RAW"),
                        new KeyValuePair<string, object?>("project", projectSlug.Value));
                }
            }
        }

        foreach (var taskId in rawTasks.Keys)
        {
            if (rawColumns.Any(c => (c.TaskIds ?? []).Contains(taskId, StringComparer.OrdinalIgnoreCase)))
                continue;

            findings.Add(new ConsistencyFinding(
                "BOARD_TASK_ORPHANED_RAW",
                ConsistencySeverity.Warning,
                $"Persisted board task '{taskId}' is not assigned to any column.",
                ConsistencyFixType.AutoRepaired,
                projectSlug.Value,
                taskId));
            AiDevTelemetry.ConsistencyRepairs.Add(1,
                new KeyValuePair<string, object?>("code", "BOARD_TASK_ORPHANED_RAW"),
                new KeyValuePair<string, object?>("project", projectSlug.Value));
        }
    }

    private void InspectDecisions(ProjectSlug projectSlug, List<ConsistencyFinding> findings, CancellationToken cancellationToken)
    {
        InspectDecisionDirectory(projectSlug, paths.DecisionsPendingDir(projectSlug), DecisionStatus.Pending, findings, cancellationToken);
        InspectDecisionDirectory(projectSlug, paths.DecisionsResolvedDir(projectSlug), DecisionStatus.Resolved, findings, cancellationToken);
    }

    private static void InspectDecisionDirectory(
        ProjectSlug projectSlug,
        DirPath directory,
        DecisionStatus expectedStatus,
        List<ConsistencyFinding> findings,
        CancellationToken cancellationToken)
    {
        if (!directory.Exists())
            return;

        foreach (var file in Directory.GetFiles(directory.Value, "*.md"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var content = File.ReadAllText(file);
                var (fields, _) = FrontmatterParser.Parse(content);
                var actualStatus = DecisionStatus.From(fields.GetValueOrDefault("status", DecisionStatus.Pending.Value));
                if (actualStatus != expectedStatus)
                {
                    findings.Add(new ConsistencyFinding(
                        "DECISION_STATUS_DIRECTORY_MISMATCH",
                        ConsistencySeverity.Warning,
                        $"Decision '{Path.GetFileName(file)}' status '{actualStatus.Value}' does not match '{expectedStatus.Value}'.",
                        ConsistencyFixType.ManualActionRequired,
                        projectSlug.Value,
                        Path.GetFileNameWithoutExtension(file)));
                }
            }
            catch (IOException ex)
            {
                findings.Add(new ConsistencyFinding(
                    "DECISION_READ_FAILED",
                    ConsistencySeverity.Error,
                    $"Failed to read decision '{Path.GetFileName(file)}': {ex.Message}",
                    ConsistencyFixType.ManualActionRequired,
                    projectSlug.Value,
                    Path.GetFileNameWithoutExtension(file)));
            }
            catch (JsonException ex)
            {
                findings.Add(new ConsistencyFinding(
                    "DECISION_PARSE_FAILED",
                    ConsistencySeverity.Error,
                    $"Failed to parse decision '{Path.GetFileName(file)}': {ex.Message}",
                    ConsistencyFixType.ManualActionRequired,
                    projectSlug.Value,
                    Path.GetFileNameWithoutExtension(file)));
            }
        }
    }

    private static IEnumerable<(ColumnId Id, string Title)> DefaultColumns()
    {
        yield return (ColumnId.Backlog, "Backlog");
        yield return (ColumnId.InProgress, "In Progress");
        yield return (ColumnId.Review, "Review");
        yield return (ColumnId.Done, "Done");
    }
}
