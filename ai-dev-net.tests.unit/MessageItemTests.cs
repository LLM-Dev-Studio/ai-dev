namespace AiDevNet.Tests.Unit;

public class MessageItemTests
{
    private static MessageItem CreateMessage() => new(
        filename: "20250101-120000-sample.md",
        agentSlug: new AgentSlug("backend-dev"),
        from: "planner",
        to: "backend-dev",
        re: "Review deployment",
        type: "task-assigned",
        body: "Please review the deployment status.");

    [Fact]
    public void Constructor_WhenFilenameMissing_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new MessageItem(
            filename: " ",
            agentSlug: new AgentSlug("backend-dev"),
            from: "planner",
            to: "backend-dev",
            re: "Review deployment",
            type: "task-assigned",
            body: "Please review the deployment status."));
    }

    [Fact]
    public void Constructor_WhenBodyMissing_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new MessageItem(
            filename: "20250101-120000-sample.md",
            agentSlug: new AgentSlug("backend-dev"),
            from: "planner",
            to: "backend-dev",
            re: "Review deployment",
            type: "task-assigned",
            body: " "));
    }

    [Fact]
    public void Constructor_NormalizesBlankPriorityToNormal()
    {
        var message = new MessageItem(
            filename: "20250101-120000-sample.md",
            agentSlug: new AgentSlug("backend-dev"),
            from: "planner",
            to: "backend-dev",
            re: "Review deployment",
            type: "task-assigned",
            body: "Please review the deployment status.",
            priority: Priority.From(" "));

        message.Priority.ShouldBe(Priority.Normal);
    }

    [Fact]
    public void Constructor_TrimsBody()
    {
        var message = new MessageItem(
            filename: "20250101-120000-sample.md",
            agentSlug: new AgentSlug("backend-dev"),
            from: "planner",
            to: "backend-dev",
            re: "Review deployment",
            type: "task-assigned",
            body: "  Please review the deployment status.  ");

        message.Body.ShouldBe("Please review the deployment status.");
    }

    [Fact]
    public void Constructor_NormalizesBlankPlaybookToNull()
    {
        var message = new MessageItem(
            filename: "20250101-120000-sample.md",
            agentSlug: new AgentSlug("backend-dev"),
            from: "planner",
            to: "backend-dev",
            re: "Review deployment",
            type: "task-assigned",
            body: "Please review the deployment status.",
            playbook: " ");

        message.Playbook.ShouldBeNull();
    }

    [Fact]
    public void Constructor_PreservesProcessedFlag()
    {
        var message = new MessageItem(
            filename: "20250101-120000-sample.md",
            agentSlug: new AgentSlug("backend-dev"),
            from: "planner",
            to: "backend-dev",
            re: "Review deployment",
            type: "task-assigned",
            body: "Please review the deployment status.",
            isProcessed: true);

        message.IsProcessed.ShouldBeTrue();
    }
}
