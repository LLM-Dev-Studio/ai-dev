using AiDev.Features.Board;
using AiDev.Models;
using AiDev.Models.Types;

namespace AiDev.Api.Routes;

public static class BoardRoutes
{
    private sealed record BoardColumnResponse(string Id, string Title, List<string> TaskIds);

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

        return app;
    }

    private static bool IsBoardInputError(string code)
        => code is "BOARD_INVALID_COLUMN" or "BOARD_UNKNOWN_COLUMN" or "BOARD_INVALID_TASK" or "BOARD_INVALID_ASSIGNEE";

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
