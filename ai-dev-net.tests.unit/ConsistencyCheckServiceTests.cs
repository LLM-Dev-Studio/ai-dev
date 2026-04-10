using Microsoft.Extensions.Logging.Abstractions;

namespace AiDevNet.Tests.Unit;

public class ConsistencyCheckServiceTests
{
    [Fact]
    public async Task CheckProjectAsync_WhenBoardIsMissingDefaultColumn_ReturnsWarning()
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var paths = new WorkspacePaths(root);
        var writer = new AtomicFileWriter();
        var workspace = new WorkspaceService(paths, writer);
        var coordinator = new ProjectMutationCoordinator();
        var boardService = new BoardService(paths, new PassingDispatcher(), writer, coordinator, NullLogger<BoardService>.Instance);
        var service = new ConsistencyCheckService(paths, workspace, boardService, NullLogger<ConsistencyCheckService>.Instance);
        workspace.CreateProject("demo-project", "Demo Project", null).ShouldBeOfType<Ok<AiDev.Models.Unit>>();

        var projectSlug = new ProjectSlug("demo-project");
        var taskId = TaskId.New();
        var board = new Board(
            projectSlug,
            [new BoardColumn(ColumnId.Backlog, "Backlog")],
            new Dictionary<TaskId, BoardTask>
            {
                [taskId] = new BoardTask(taskId, "Investigate failure")
            });
        boardService.SaveBoard(projectSlug, board);

        var report = await service.CheckProjectAsync(projectSlug, TestContext.Current.CancellationToken);

        report.Findings.ShouldContain(f => f.Code == "BOARD_COLUMN_MISSING_RAW");
        report.Findings.ShouldContain(f => f.Code == "BOARD_TASK_ORPHANED_RAW");
    }

    [Fact]
    public async Task CheckProjectAsync_WhenDecisionStatusMismatchesDirectory_ReturnsWarning()
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var paths = new WorkspacePaths(root);
        var writer = new AtomicFileWriter();
        var workspace = new WorkspaceService(paths, writer);
        var coordinator = new ProjectMutationCoordinator();
        var boardService = new BoardService(paths, new PassingDispatcher(), writer, coordinator, NullLogger<BoardService>.Instance);
        var service = new ConsistencyCheckService(paths, workspace, boardService, NullLogger<ConsistencyCheckService>.Instance);
        workspace.CreateProject("demo-project", "Demo Project", null).ShouldBeOfType<Ok<AiDev.Models.Unit>>();

        var projectSlug = new ProjectSlug("demo-project");
        Directory.CreateDirectory(paths.DecisionsPendingDir(projectSlug));
        File.WriteAllText(Path.Combine(paths.DecisionsPendingDir(projectSlug), "20260101-000000-test.md"), "---\nstatus: resolved\nsubject: Test\nfrom: overwatch\npriority: normal\n---\n\nBody");

        var report = await service.CheckProjectAsync(projectSlug, TestContext.Current.CancellationToken);

        report.Findings.ShouldContain(f => f.Code == "DECISION_STATUS_DIRECTORY_MISMATCH");
    }

    private sealed class PassingDispatcher : IDomainEventDispatcher
    {
        public Task<Result<AiDev.Models.Unit>> Dispatch(IReadOnlyList<DomainEvent> events, CancellationToken ct = default)
            => Task.FromResult<Result<AiDev.Models.Unit>>(new Ok<AiDev.Models.Unit>(AiDev.Models.Unit.Value));
    }
}
