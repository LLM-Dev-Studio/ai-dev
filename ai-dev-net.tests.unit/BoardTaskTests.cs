namespace AiDevNet.Tests.Unit;

public class BoardTaskTests
{
    private static TaskId NewTaskId() => TaskId.New();

    private static BoardTask CreateTask() => new(
        id: NewTaskId(),
        title: "Investigate failure",
        priority: Priority.High,
        description: "Check recent deploy changes",
        assignee: "backend-dev");

    [Fact]
    public void Constructor_WhenTitleMissing_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new BoardTask(
            id: NewTaskId(),
            title: " "));
    }

    [Fact]
    public void Constructor_NormalizesBlankPriorityToNormal()
    {
        var task = new BoardTask(
            id: NewTaskId(),
            title: "Investigate failure",
            priority: Priority.From(" "));

        task.Priority.ShouldBe(Priority.Normal);
    }

    [Fact]
    public void Constructor_NormalizesBlankDescriptionToNull()
    {
        var task = new BoardTask(
            id: NewTaskId(),
            title: "Investigate failure",
            description: " ");

        task.Description.ShouldBeNull();
    }

    [Fact]
    public void UpdateDetails_UpdatesNormalizedValues()
    {
        var task = CreateTask();

        task.UpdateDetails("Refine prompt", Priority.From(" "), " ", "frontend-dev");

        task.Title.ShouldBe("Refine prompt");
        task.Priority.ShouldBe(Priority.Normal);
        task.Description.ShouldBeNull();
        task.Assignee.ShouldBe("frontend-dev");
    }

    [Fact]
    public void MoveToColumn_WhenDone_SetsCompletedAt()
    {
        var task = CreateTask();
        var movedAt = DateTime.UtcNow;

        task.MoveToColumn(ColumnId.Done, movedAt);

        task.CompletedAt.ShouldBe(movedAt);
    }

    [Fact]
    public void MoveToColumn_WhenNotDone_ClearsCompletedAt()
    {
        var task = new BoardTask(
            id: NewTaskId(),
            title: "Investigate failure",
            completedAt: DateTime.UtcNow.AddMinutes(-5));

        task.MoveToColumn(new ColumnId("doing"), DateTime.UtcNow);

        task.CompletedAt.ShouldBeNull();
    }

    [Fact]
    public void MoveToColumn_ClearsNudgedAt()
    {
        var task = new BoardTask(
            id: NewTaskId(),
            title: "Investigate failure",
            nudgedAt: DateTime.UtcNow.AddMinutes(-2));

        task.MoveToColumn(new ColumnId("doing"), DateTime.UtcNow);

        task.NudgedAt.ShouldBeNull();
    }

    [Fact]
    public void MarkNudged_SetsNudgedAt()
    {
        var task = CreateTask();
        var nudgedAt = DateTime.UtcNow;

        task.MarkNudged(nudgedAt);

        task.NudgedAt.ShouldBe(nudgedAt);
    }
}
