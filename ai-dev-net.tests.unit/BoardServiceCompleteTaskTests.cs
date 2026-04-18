using AiDev.Models;

using Microsoft.Extensions.Logging.Abstractions;

namespace AiDevNet.Tests.Unit;

public class BoardServiceCompleteTaskTests
{
    private static readonly ProjectSlug Project = new("test-project");

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static BoardService CreateService()
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var paths = new WorkspacePaths(root);
        var dispatcher = Substitute.For<IDomainEventDispatcher>();
        dispatcher.Dispatch(Arg.Any<IReadOnlyList<DomainEvent>>(), Arg.Any<CancellationToken>())
            .Returns(new Ok<AiDev.Models.Unit>(AiDev.Models.Unit.Value));
        return new BoardService(
            paths,
            dispatcher,
            new AtomicFileWriter(),
            new ProjectMutationCoordinator(),
            NullLogger<BoardService>.Instance,
            new ProjectStateChangedNotifier());
    }

    private static SessionResult MakeResult(string taskId, IReadOnlyList<string>? tags = null) =>
        new(TaskId: taskId,
            Status: "completed",
            Summary: "Done",
            PullRequestUrl: null,
            FilesChanged: [],
            TestOutcome: null,
            CompletedAt: DateTime.UtcNow,
            Tags: tags);

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CompleteTaskFromResult_HappyPath_MovesTaskToDone()
    {
        var svc = CreateService();
        var taskId = TaskId.New();

        // Seed board with one task in Backlog.
        var createResult = await svc.CreateTaskAsync(Project, ColumnId.Backlog.Value, "Ship it", null, "normal", null);
        createResult.ShouldBeOfType<Ok<BoardTask>>();
        var createdTask = ((Ok<BoardTask>)createResult).Value;

        svc.CompleteTaskFromResult(Project, createdTask.Id, MakeResult(createdTask.Id.Value));

        var board = svc.LoadBoard(Project);
        board.Columns.Single(c => c.Id == ColumnId.Done).TaskIds.ShouldContain(createdTask.Id);
        board.Columns.Single(c => c.Id == ColumnId.Backlog).TaskIds.ShouldNotContain(createdTask.Id);
    }

    [Fact]
    public async Task CompleteTaskFromResult_MergesTags()
    {
        var svc = CreateService();

        var createResult = await svc.CreateTaskAsync(Project, ColumnId.Backlog.Value, "Ship it", null, "normal", null, ["existing"]);
        var createdTask = ((Ok<BoardTask>)createResult).Value;

        svc.CompleteTaskFromResult(Project, createdTask.Id, MakeResult(createdTask.Id.Value, ["new-tag"]));

        var board = svc.LoadBoard(Project);
        var task = board.Tasks[createdTask.Id];
        task.Tags.ShouldContain("existing");
        task.Tags.ShouldContain("new-tag");
    }

    [Fact]
    public void CompleteTaskFromResult_UnknownTaskId_IsNoOp()
    {
        var svc = CreateService();
        var unknownId = TaskId.New();

        // Should not throw; board has no tasks.
        Should.NotThrow(() =>
            svc.CompleteTaskFromResult(Project, unknownId, MakeResult(unknownId.Value)));
    }

    [Fact]
    public async Task CompleteTaskFromResult_AlreadyDone_IsNoOp()
    {
        var svc = CreateService();

        var createResult = await svc.CreateTaskAsync(Project, ColumnId.Backlog.Value, "Ship it", null, "normal", null);
        var createdTask = ((Ok<BoardTask>)createResult).Value;

        // Move to Done first.
        svc.CompleteTaskFromResult(Project, createdTask.Id, MakeResult(createdTask.Id.Value));

        // Board state before second call.
        var boardBefore = svc.LoadBoard(Project);
        var doneBefore = boardBefore.Tasks[createdTask.Id].CompletedAt;

        // Second call should not change CompletedAt.
        svc.CompleteTaskFromResult(Project, createdTask.Id, MakeResult(createdTask.Id.Value));

        var boardAfter = svc.LoadBoard(Project);
        boardAfter.Tasks[createdTask.Id].CompletedAt.ShouldBe(doneBefore);
    }
}
