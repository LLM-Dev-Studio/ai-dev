using System.Text.Json;

using AiDev.Models;

namespace AiDevNet.Tests.Unit;

public class SessionResultTests
{
    [Fact]
    public void Deserialize_FullPayload_ReadsAllFields()
    {
        var json = """
            {
              "taskId": "TASK-42",
              "status": "completed",
              "summary": "Shipped the feature",
              "pullRequestUrl": "https://github.com/org/repo/pull/99",
              "filesChanged": ["src/Foo.cs", "src/Bar.cs"],
              "testOutcome": "passed",
              "completedAt": "2026-04-18T12:00:00Z",
              "tags": ["feat", "api"]
            }
            """;

        var result = JsonSerializer.Deserialize<SessionResult>(json, JsonDefaults.Read);

        result.ShouldNotBeNull();
        result!.TaskId.ShouldBe("TASK-42");
        result.Status.ShouldBe("completed");
        result.Summary.ShouldBe("Shipped the feature");
        result.PullRequestUrl.ShouldBe("https://github.com/org/repo/pull/99");
        result.FilesChanged.ShouldBe(["src/Foo.cs", "src/Bar.cs"]);
        result.TestOutcome.ShouldBe("passed");
        result.CompletedAt.ShouldNotBeNull();
        result.Tags.ShouldBe(["feat", "api"]);
    }

    [Fact]
    public void Deserialize_MinimalPayload_NullablesAreNull()
    {
        var json = """
            {
              "taskId": null,
              "status": "partial",
              "summary": null,
              "pullRequestUrl": null,
              "filesChanged": [],
              "testOutcome": null,
              "completedAt": null
            }
            """;

        var result = JsonSerializer.Deserialize<SessionResult>(json, JsonDefaults.Read);

        result.ShouldNotBeNull();
        result!.TaskId.ShouldBeNull();
        result.Tags.ShouldBeNull();
        result.FilesChanged.ShouldBeEmpty();
    }

    [Fact]
    public void Deserialize_CamelCaseKeys_RoundTrips()
    {
        var original = new SessionResult(
            TaskId: "TASK-1",
            Status: "completed",
            Summary: "done",
            PullRequestUrl: null,
            FilesChanged: [],
            TestOutcome: null,
            CompletedAt: null,
            Tags: ["backend"]);

        var json = JsonSerializer.Serialize(original, JsonDefaults.Write);
        var roundTripped = JsonSerializer.Deserialize<SessionResult>(json, JsonDefaults.Read);

        roundTripped.ShouldNotBeNull();
        roundTripped!.TaskId.ShouldBe("TASK-1");
        roundTripped.Tags.ShouldBe(["backend"]);
    }
}
