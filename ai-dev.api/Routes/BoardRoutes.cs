using AiDev.Features.Board;
using AiDev.Models;
using AiDev.Models.Types;

namespace AiDev.Api.Routes;

public static class BoardRoutes
{
    private sealed record BoardTaskResponse(
        string Id,
        string Title,
        string Priority,
        string? Description,
        string? Assignee,
        List<string>? Tags,
        DateTime? CreatedAt,
        DateTime? CompletedAt,
        DateTime? MovedAt,
        DateTime? NudgedAt);

    private sealed record BoardResponse(List<BoardColumnResponse> Columns, Dictionary<string, BoardTaskResponse> Tasks);

    private sealed record AddColumnRequest(string Id, string Title);
    private sealed record RenameColumnRequest(string Title);

    private sealed record BoardColumnResponse(string Id, string Title, List<string> TaskIds);

    private sealed record CreateTaskRequest(
        string? ColumnId,
        string Title,
        string? Description,
        string? Priority,
        string? Assignee,
        List<string>? Tags);

    private sealed record UpdateTaskRequest(
        string ColumnId,
        string Title,
        string? Description,
        string? Priority,
        string? Assignee,
        List<string>? Tags);

    public static IEndpointRouteBuilder MapBoardRoutes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/board", (string projectSlug, IBoardService boardService) =>
        {
            ProjectSlug project = projectSlug;
            var board = boardService.LoadBoard(project);
            return Results.Ok(ToResponse(board));
        });

        app.MapPost("/api/board/tasks", async (string projectSlug, CreateTaskRequest body, IBoardService boardService, CancellationToken ct) =>
        {
            ProjectSlug project = projectSlug;
            var columnId = string.IsNullOrWhiteSpace(body.ColumnId) ? ColumnId.Backlog.Value : body.ColumnId;
            var priority = string.IsNullOrWhiteSpace(body.Priority) ? "normal" : body.Priority;

            var result = await boardService.CreateTaskAsync(
                project,
                columnId,
                body.Title,
                body.Description,
                priority,
                body.Assignee,
                body.Tags,
                ct);

            return result switch
            {
                Ok<BoardTask> ok => Results.Ok(ToTaskResponse(ok.Value)),
                Err<BoardTask> err when IsBoardInputError(err.Error.Code) => Results.BadRequest(err.Error),
                Err<BoardTask> err => Results.Problem(err.Error.Message),
                _ => Results.Problem("Unexpected result."),
            };
        });

        app.MapPost("/api/board/tasks/{taskId}", async (string taskId, string projectSlug, UpdateTaskRequest body, IBoardService boardService, CancellationToken ct) =>
        {
            ProjectSlug project = projectSlug;
            TaskId id = taskId;
            var priority = string.IsNullOrWhiteSpace(body.Priority) ? "normal" : body.Priority;

            var result = await boardService.UpdateTaskAsync(
                project,
                id,
                body.ColumnId,
                body.Title,
                body.Description,
                priority,
                body.Assignee,
                body.Tags,
                ct);

            return result switch
            {
                Ok<BoardTask> ok => Results.Ok(ToTaskResponse(ok.Value)),
                Err<BoardTask> err when err.Error.Code == "BOARD_TASK_NOT_FOUND" => Results.NotFound(),
                Err<BoardTask> err when IsBoardInputError(err.Error.Code) => Results.BadRequest(err.Error),
                Err<BoardTask> err => Results.Problem(err.Error.Message),
                _ => Results.Problem("Unexpected result."),
            };
        });

        app.MapDelete("/api/board/tasks/{taskId}", async (string taskId, string projectSlug, IBoardService boardService, CancellationToken ct) =>
        {
            ProjectSlug project = projectSlug;
            TaskId id = taskId;

            var result = await boardService.DeleteTaskAsync(project, id, ct);
            return result switch
            {
                Ok<Unit> => Results.Ok(),
                Err<Unit> err when err.Error.Code == "BOARD_TASK_NOT_FOUND" => Results.NotFound(),
                Err<Unit> err => Results.Problem(err.Error.Message),
                _ => Results.Problem("Unexpected result."),
            };
        });

        app.MapPost("/api/board/columns", async (string projectSlug, AddColumnRequest body, IBoardService boardService, CancellationToken ct) =>
        {
            ProjectSlug project = projectSlug;
            var result = await boardService.AddColumnAsync(project, body.Id, body.Title, ct);
            return result switch
            {
                Ok<BoardColumn> ok => Results.Ok(new BoardColumnResponse(ok.Value.Id.Value, ok.Value.Title, [])),
                Err<BoardColumn> err when IsColumnInputError(err.Error.Code) => Results.BadRequest(err.Error),
                Err<BoardColumn> err => Results.Problem(err.Error.Message),
                _ => Results.Problem("Unexpected result."),
            };
        });

        app.MapPatch("/api/board/columns/{columnId}", async (string columnId, string projectSlug, RenameColumnRequest body, IBoardService boardService, CancellationToken ct) =>
        {
            ProjectSlug project = projectSlug;
            if (!ColumnId.TryParse(columnId, out var parsedColumnId))
                return Results.BadRequest(new DomainError("BOARD_INVALID_COLUMN", "Column id is invalid."));

            var result = await boardService.RenameColumnAsync(project, parsedColumnId, body.Title, ct);
            return result switch
            {
                Ok<Unit> => Results.Ok(),
                Err<Unit> err when err.Error.Code == "BOARD_UNKNOWN_COLUMN" => Results.NotFound(),
                Err<Unit> err when IsColumnInputError(err.Error.Code) => Results.BadRequest(err.Error),
                Err<Unit> err => Results.Problem(err.Error.Message),
                _ => Results.Problem("Unexpected result."),
            };
        });

        app.MapDelete("/api/board/columns/{columnId}", async (string columnId, string projectSlug, IBoardService boardService, CancellationToken ct) =>
        {
            ProjectSlug project = projectSlug;
            if (!ColumnId.TryParse(columnId, out var parsedColumnId))
                return Results.BadRequest(new DomainError("BOARD_INVALID_COLUMN", "Column id is invalid."));

            var result = await boardService.RemoveColumnAsync(project, parsedColumnId, ct);
            return result switch
            {
                Ok<Unit> => Results.Ok(),
                Err<Unit> err when err.Error.Code == "BOARD_UNKNOWN_COLUMN" => Results.NotFound(),
                Err<Unit> err when IsColumnInputError(err.Error.Code) => Results.BadRequest(err.Error),
                Err<Unit> err => Results.Problem(err.Error.Message),
                _ => Results.Problem("Unexpected result."),
            };
        });

        return app;
    }

    private static bool IsBoardInputError(string code)
        => code is "BOARD_INVALID_COLUMN" or "BOARD_UNKNOWN_COLUMN" or "BOARD_INVALID_TASK" or "BOARD_INVALID_ASSIGNEE";

    private static bool IsColumnInputError(string code)
        => code is "BOARD_INVALID_COLUMN" or "BOARD_DUPLICATE_COLUMN" or "BOARD_PROTECTED_COLUMN"
                or "BOARD_COLUMN_NOT_EMPTY" or "BOARD_INVALID_COLUMN_TITLE";

    private static BoardResponse ToResponse(Board board)
        => new(
            Columns: [.. board.Columns.Select(column => new BoardColumnResponse(
                column.Id.Value,
                column.Title,
                [.. column.TaskIds.Select(taskId => taskId.Value)]))],
            Tasks: board.Tasks.ToDictionary(
                kv => kv.Key.Value,
                kv => ToTaskResponse(kv.Value)));

    private static BoardTaskResponse ToTaskResponse(BoardTask task)
        => new(
            task.Id.Value,
            task.Title,
            task.Priority.Value,
            task.Description,
            task.Assignee,
            task.Tags.Count > 0 ? [.. task.Tags] : null,
            task.CreatedAt,
            task.CompletedAt,
            task.MovedAt,
            task.NudgedAt);
}
