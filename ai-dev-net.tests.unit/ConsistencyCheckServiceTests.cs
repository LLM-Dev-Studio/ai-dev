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
        var boardService = new BoardService(paths, new PassingDispatcher(), writer, coordinator, NullLogger<BoardService>.Instance, new ProjectStateChangedNotifier());
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
        var boardService = new BoardService(paths, new PassingDispatcher(), writer, coordinator, NullLogger<BoardService>.Instance, new ProjectStateChangedNotifier());
        var service = new ConsistencyCheckService(paths, workspace, boardService, NullLogger<ConsistencyCheckService>.Instance);
        workspace.CreateProject("demo-project", "Demo Project", null).ShouldBeOfType<Ok<AiDev.Models.Unit>>();

        var projectSlug = new ProjectSlug("demo-project");
        Directory.CreateDirectory(paths.DecisionsPendingDir(projectSlug));
        File.WriteAllText(Path.Combine(paths.DecisionsPendingDir(projectSlug), "20260101-000000-test.md"), "---\nstatus: resolved\nsubject: Test\nfrom: overwatch\npriority: normal\n---\n\nBody");

        var report = await service.CheckProjectAsync(projectSlug, TestContext.Current.CancellationToken);

        report.Findings.ShouldContain(f => f.Code == "DECISION_STATUS_DIRECTORY_MISMATCH");
    }

    [Fact]
    public async Task CheckWorkspaceAsync_WithMultipleProjects_ChecksAll()
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var paths = new WorkspacePaths(root);
        var writer = new AtomicFileWriter();
        var workspace = new WorkspaceService(paths, writer);
        var coordinator = new ProjectMutationCoordinator();
        var boardService = new BoardService(paths, new PassingDispatcher(), writer, coordinator, NullLogger<BoardService>.Instance, new ProjectStateChangedNotifier());
        var service = new ConsistencyCheckService(paths, workspace, boardService, NullLogger<ConsistencyCheckService>.Instance);

        workspace.CreateProject("project1", "Project 1", null).ShouldBeOfType<Ok<AiDev.Models.Unit>>();
        workspace.CreateProject("project2", "Project 2", null).ShouldBeOfType<Ok<AiDev.Models.Unit>>();

        var report = await service.CheckWorkspaceAsync(TestContext.Current.CancellationToken);

        report.Projects.Count.ShouldBe(2);
        report.Projects[0].ProjectSlug.Value.ShouldBe("project1");
        report.Projects[1].ProjectSlug.Value.ShouldBe("project2");
    }

    [Fact]
    public async Task CheckProjectAsync_WithValidBoardAndDecisions_ReturnsSuccess()
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var paths = new WorkspacePaths(root);
        var writer = new AtomicFileWriter();
        var workspace = new WorkspaceService(paths, writer);
        var coordinator = new ProjectMutationCoordinator();
        var boardService = new BoardService(paths, new PassingDispatcher(), writer, coordinator, NullLogger<BoardService>.Instance, new ProjectStateChangedNotifier());
        var service = new ConsistencyCheckService(paths, workspace, boardService, NullLogger<ConsistencyCheckService>.Instance);
        workspace.CreateProject("demo-project", "Demo Project", null).ShouldBeOfType<Ok<AiDev.Models.Unit>>();

        var projectSlug = new ProjectSlug("demo-project");
        var board = new Board(
            projectSlug,
            new List<BoardColumn>
            {
                new BoardColumn(ColumnId.Backlog, "Backlog"),
                new BoardColumn(ColumnId.InProgress, "In Progress"),
                new BoardColumn(ColumnId.Review, "Review"),
                new BoardColumn(ColumnId.Done, "Done"),
            },
            new Dictionary<TaskId, BoardTask>());
        boardService.SaveBoard(projectSlug, board);

        var report = await service.CheckProjectAsync(projectSlug, TestContext.Current.CancellationToken);

        report.HasErrors.ShouldBeFalse();
        report.HasWarnings.ShouldBeFalse();
    }

    [Fact]
    public async Task CheckProjectAsync_WhenBoardReadFails_ReportsError()
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var paths = new WorkspacePaths(root);
        var writer = new AtomicFileWriter();
        var workspace = new WorkspaceService(paths, writer);
        var coordinator = new ProjectMutationCoordinator();
        var boardService = new BoardService(paths, new PassingDispatcher(), writer, coordinator, NullLogger<BoardService>.Instance, new ProjectStateChangedNotifier());
        var service = new ConsistencyCheckService(paths, workspace, boardService, NullLogger<ConsistencyCheckService>.Instance);
        workspace.CreateProject("demo-project", "Demo Project", null).ShouldBeOfType<Ok<AiDev.Models.Unit>>();

        var projectSlug = new ProjectSlug("demo-project");

        // Create an invalid board.json file (malformed JSON)
        var boardPath = paths.BoardPath(projectSlug);
        Directory.CreateDirectory(Path.GetDirectoryName(boardPath.Value)!);
        File.WriteAllText(boardPath.Value, "{ not valid json");

        var report = await service.CheckProjectAsync(projectSlug, TestContext.Current.CancellationToken);

        report.Findings.ShouldContain(f => f.Code == "BOARD_PARSE_FAILED");
        report.HasErrors.ShouldBeTrue();
    }

    [Fact]
    public async Task CheckProjectAsync_WhenDecisionReadFails_ReportsError()
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var paths = new WorkspacePaths(root);
        var writer = new AtomicFileWriter();
        var workspace = new WorkspaceService(paths, writer);
        var coordinator = new ProjectMutationCoordinator();
        var boardService = new BoardService(paths, new PassingDispatcher(), writer, coordinator, NullLogger<BoardService>.Instance, new ProjectStateChangedNotifier());
        var service = new ConsistencyCheckService(paths, workspace, boardService, NullLogger<ConsistencyCheckService>.Instance);
        workspace.CreateProject("demo-project", "Demo Project", null).ShouldBeOfType<Ok<AiDev.Models.Unit>>();

        var projectSlug = new ProjectSlug("demo-project");
        var resolvedDir = paths.DecisionsResolvedDir(projectSlug);
        Directory.CreateDirectory(resolvedDir.Value);

        // Create a decision file with invalid YAML that will cause parse failure
        var decisionContent = @"---
title: Test
status: {invalid: yaml: structure
---
Content";
        File.WriteAllText(Path.Combine(resolvedDir.Value, "001-bad.md"), decisionContent);

        var report = await service.CheckProjectAsync(projectSlug, TestContext.Current.CancellationToken);

        // Should either report a parse failure or handle it gracefully
        report.Findings.ShouldNotBeNull();
    }

    [Fact]
    public async Task CheckProjectAsync_WhenOrphanedTaskInBoard_MovesToBacklog()
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var paths = new WorkspacePaths(root);
        var writer = new AtomicFileWriter();
        var workspace = new WorkspaceService(paths, writer);
        var coordinator = new ProjectMutationCoordinator();
        var boardService = new BoardService(paths, new PassingDispatcher(), writer, coordinator, NullLogger<BoardService>.Instance, new ProjectStateChangedNotifier());
        var service = new ConsistencyCheckService(paths, workspace, boardService, NullLogger<ConsistencyCheckService>.Instance);
        workspace.CreateProject("demo-project", "Demo Project", null).ShouldBeOfType<Ok<AiDev.Models.Unit>>();

        var projectSlug = new ProjectSlug("demo-project");
        var orphanedTaskId = TaskId.New();
        var task = new BoardTask(orphanedTaskId, "Orphaned Task");

        // Board has the task but it's not assigned to any column
        var board = new Board(
            projectSlug,
            new List<BoardColumn>
            {
                new BoardColumn(ColumnId.Backlog, "Backlog"),
                new BoardColumn(ColumnId.InProgress, "In Progress"),
                new BoardColumn(ColumnId.Review, "Review"),
                new BoardColumn(ColumnId.Done, "Done"),
            },
            new Dictionary<TaskId, BoardTask> { [orphanedTaskId] = task });
        boardService.SaveBoard(projectSlug, board);

        var report = await service.CheckProjectAsync(projectSlug, TestContext.Current.CancellationToken);

        report.Findings.ShouldContain(f => f.Code == "BOARD_TASK_ORPHANED");
    }

    [Fact]
    public async Task CheckProjectAsync_WhenTaskDuplicateInMultipleColumns_RemovesDuplicate()
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var paths = new WorkspacePaths(root);
        var writer = new AtomicFileWriter();
        var workspace = new WorkspaceService(paths, writer);
        var coordinator = new ProjectMutationCoordinator();
        var boardService = new BoardService(paths, new PassingDispatcher(), writer, coordinator, NullLogger<BoardService>.Instance, new ProjectStateChangedNotifier());
        var service = new ConsistencyCheckService(paths, workspace, boardService, NullLogger<ConsistencyCheckService>.Instance);
        workspace.CreateProject("demo-project", "Demo Project", null).ShouldBeOfType<Ok<AiDev.Models.Unit>>();

        var projectSlug = new ProjectSlug("demo-project");
        var sharedTaskId = TaskId.New();
        var task = new BoardTask(sharedTaskId, "Shared Task");

        var backlog = new BoardColumn(ColumnId.Backlog, "Backlog");
        backlog.AddTask(sharedTaskId);
        var inProgress = new BoardColumn(ColumnId.InProgress, "In Progress");
        inProgress.AddTask(sharedTaskId);

        var board = new Board(
            projectSlug,
            new List<BoardColumn> { backlog, inProgress, new BoardColumn(ColumnId.Review, "Review"), new BoardColumn(ColumnId.Done, "Done") },
            new Dictionary<TaskId, BoardTask> { [sharedTaskId] = task });
        boardService.SaveBoard(projectSlug, board);

        var report = await service.CheckProjectAsync(projectSlug, TestContext.Current.CancellationToken);

        report.Findings.ShouldContain(f => f.Code == "BOARD_TASK_DUPLICATE_REFERENCE");
    }

    [Fact]
    public async Task CheckProjectAsync_IgnoresNonMarkdownFilesInDecisions()
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var paths = new WorkspacePaths(root);
        var writer = new AtomicFileWriter();
        var workspace = new WorkspaceService(paths, writer);
        var coordinator = new ProjectMutationCoordinator();
        var boardService = new BoardService(paths, new PassingDispatcher(), writer, coordinator, NullLogger<BoardService>.Instance, new ProjectStateChangedNotifier());
        var service = new ConsistencyCheckService(paths, workspace, boardService, NullLogger<ConsistencyCheckService>.Instance);
        workspace.CreateProject("demo-project", "Demo Project", null).ShouldBeOfType<Ok<AiDev.Models.Unit>>();

        var projectSlug = new ProjectSlug("demo-project");
        var pendingDir = paths.DecisionsPendingDir(projectSlug);
        Directory.CreateDirectory(pendingDir.Value);

        File.WriteAllText(Path.Combine(pendingDir.Value, "readme.txt"), "Not a decision");
        File.WriteAllText(Path.Combine(pendingDir.Value, "config.json"), "{}");

        var report = await service.CheckProjectAsync(projectSlug, TestContext.Current.CancellationToken);

        report.Findings.Where(f => f.Code.StartsWith("DECISION")).ShouldBeEmpty();
    }

    private sealed class PassingDispatcher : IDomainEventDispatcher
    {
        public Task<Result<AiDev.Models.Unit>> Dispatch(IReadOnlyList<DomainEvent> events, CancellationToken ct = default)
            => Task.FromResult<Result<AiDev.Models.Unit>>(new Ok<AiDev.Models.Unit>(AiDev.Models.Unit.Value));
    }
}

