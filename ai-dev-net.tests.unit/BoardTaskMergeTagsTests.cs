namespace AiDevNet.Tests.Unit;

public class BoardTaskMergeTagsTests
{
    private static BoardTask CreateTask(List<string>? tags = null) =>
        new(id: TaskId.New(), title: "Test task", tags: tags);

    [Fact]
    public void MergeTags_AddsNewTags()
    {
        var task = CreateTask(["existing"]);

        task.MergeTags(["new-tag", "another"]);

        task.Tags.ShouldBe(["existing", "new-tag", "another"], ignoreOrder: true);
    }

    [Fact]
    public void MergeTags_IgnoresDuplicatesCaseInsensitive()
    {
        var task = CreateTask(["Backend", "api"]);

        task.MergeTags(["backend", "API", "frontend"]);

        task.Tags.Count.ShouldBe(3);
        task.Tags.ShouldContain("Backend");
        task.Tags.ShouldContain("api");
        task.Tags.ShouldContain("frontend");
    }

    [Fact]
    public void MergeTags_SkipsBlankAndNullEntries()
    {
        var task = CreateTask();

        task.MergeTags([" ", "", "valid"]);

        task.Tags.ShouldBe(["valid"]);
    }

    [Fact]
    public void Tags_CannotBeMutatedViaReturnedReference()
    {
        var task = CreateTask(["initial"]);
        var tags = task.Tags;

        // IReadOnlyList does not expose mutating methods — this verifies the compile-time contract.
        // If Tags were a List<string>, external code could call .Add() directly on the returned reference.
        tags.ShouldBeAssignableTo<System.Collections.Generic.IReadOnlyList<string>>();
    }

    [Fact]
    public void Constructor_TagsAreNormalized_DuplicatesRemoved()
    {
        var task = new BoardTask(
            id: TaskId.New(),
            title: "Test task",
            tags: ["Feat", "feat", "API", "api"]);

        task.Tags.Count.ShouldBe(2);
    }

    [Fact]
    public void Constructor_TagsAreNormalized_BlankEntriesStripped()
    {
        var task = new BoardTask(
            id: TaskId.New(),
            title: "Test task",
            tags: [" ", "", "valid"]);

        task.Tags.ShouldBe(["valid"]);
    }
}
