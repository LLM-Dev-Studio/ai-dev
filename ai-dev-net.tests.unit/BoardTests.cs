namespace AiDevNet.Tests.Unit;

public class BoardTests
{
    private static Board CreateBoard() => new(
        columns:
        [
            new BoardColumn(ColumnId.Backlog, "Backlog"),
            new BoardColumn(ColumnId.Done, "Done")
        ]);

    [Fact]
    public void Constructor_InitializesEmptyCollections()
    {
        var board = new Board();

        board.Columns.Select(c => c.Id).ShouldBe([ColumnId.Backlog, ColumnId.InProgress, ColumnId.Review, ColumnId.Done]);
        board.Tasks.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenColumnsEmpty_InitializesDefaultColumns()
    {
        var board = new Board(columns: [], tasks: []);

        board.Columns.Select(c => c.Id).ShouldBe([ColumnId.Backlog, ColumnId.InProgress, ColumnId.Review, ColumnId.Done]);
    }

    [Fact]
    public void AddTask_WhenColumnMissing_ReturnsError()
    {
        var board = CreateBoard();
        var task = new BoardTask(TaskId.New(), "Investigate failure");

        var result = board.AddTask(new ColumnId("review"), task);

        result.ShouldBeOfType<Err<BoardTask>>();
    }

    [Fact]
    public void AddTask_WhenAssigneePresent_RaisesTaskAssignedEvent()
    {
        var board = CreateBoard();
        var task = new BoardTask(TaskId.New(), "Investigate failure", assignee: "backend-dev");

        var result = board.AddTask(ColumnId.Backlog, task);
        var domainEvent = board.DequeueDomainEvents().Single();

        result.ShouldBeOfType<Ok<BoardTask>>();
        domainEvent.ShouldBeOfType<TaskAssigned>();
    }

    [Fact]
    public void UpdateTask_WhenMovedToDone_UpdatesColumnMembership()
    {
        var board = CreateBoard();
        var task = new BoardTask(TaskId.New(), "Investigate failure");
        board.AddTask(ColumnId.Backlog, task);

        var result = board.UpdateTask(
            task.Id,
            ColumnId.Done,
            task.Title,
            Priority.Normal,
            task.Description,
            task.Assignee,
            DateTime.UtcNow);

        result.ShouldBeOfType<Ok<BoardTask>>();
        board.Columns.Single(c => c.Id == ColumnId.Backlog).TaskIds.ShouldNotContain(task.Id);
        board.Columns.Single(c => c.Id == ColumnId.Done).TaskIds.ShouldContain(task.Id);
    }

    [Fact]
    public void DeleteTask_WhenPresent_RemovesTaskFromBoard()
    {
        var board = CreateBoard();
        var task = new BoardTask(TaskId.New(), "Investigate failure");
        board.AddTask(ColumnId.Backlog, task);

        var result = board.DeleteTask(task.Id);

        result.ShouldBeOfType<Ok<AiDev.Models.Unit>>();
        board.Tasks.ContainsKey(task.Id).ShouldBeFalse();
        board.Columns.Single(c => c.Id == ColumnId.Backlog).TaskIds.ShouldNotContain(task.Id);
    }
}
