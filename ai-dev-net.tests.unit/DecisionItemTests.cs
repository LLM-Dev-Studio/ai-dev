namespace AiDevNet.Tests.Unit;

public class DecisionItemTests
{
    private static DecisionItem CreateDecision() => new(
        filename: "20250101-120000-need-human-input.md",
        id: "20250101-120000-need-human-input",
        from: "overwatch",
        subject: "Need human input",
        body: "Please decide how to proceed.");

    [Fact]
    public void Constructor_WhenFilenameMissing_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new DecisionItem(
            filename: " ",
            id: "20250101-120000-need-human-input",
            from: "overwatch",
            subject: "Need human input",
            body: "Please decide how to proceed."));
    }

    [Fact]
    public void Constructor_WhenBodyMissing_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new DecisionItem(
            filename: "20250101-120000-need-human-input.md",
            id: "20250101-120000-need-human-input",
            from: "overwatch",
            subject: "Need human input",
            body: " "));
    }

    [Fact]
    public void Constructor_NormalizesBlankPriorityToNormal()
    {
        var decision = new DecisionItem(
            filename: "20250101-120000-need-human-input.md",
            id: "20250101-120000-need-human-input",
            from: "overwatch",
            subject: "Need human input",
            body: "Please decide how to proceed.",
            priority: Priority.From(" "));

        decision.Priority.ShouldBe(Priority.Normal);
    }

    [Fact]
    public void Constructor_NormalizesBlankStatusToPending()
    {
        var decision = new DecisionItem(
            filename: "20250101-120000-need-human-input.md",
            id: "20250101-120000-need-human-input",
            from: "overwatch",
            subject: "Need human input",
            body: "Please decide how to proceed.",
            status: DecisionStatus.From(" "));

        decision.Status.ShouldBe(DecisionStatus.Pending);
    }

    [Fact]
    public void Resolve_WhenPending_UpdatesResolutionFields()
    {
        var decision = CreateDecision();
        var resolvedAt = DateTime.UtcNow;

        decision.Resolve("human", "Proceed with the rollout.", resolvedAt);

        decision.Status.ShouldBe(DecisionStatus.Resolved);
        decision.ResolvedBy.ShouldBe("human");
        decision.ResolvedAt.ShouldBe(resolvedAt);
        decision.Response.ShouldBe("Proceed with the rollout.");
    }

    [Fact]
    public void Resolve_WhenPending_RaisesDecisionResolvedEvent()
    {
        var decision = CreateDecision();
        var resolvedAt = DateTime.UtcNow;

        decision.Resolve("human", "Proceed with the rollout.", resolvedAt);
        var domainEvent = decision.DequeueDomainEvents().Single();

        domainEvent.ShouldBeOfType<DecisionResolved>();
    }

    [Fact]
    public void Resolve_WhenAlreadyResolved_ThrowsInvalidOperationException()
    {
        var decision = new DecisionItem(
            filename: "20250101-120000-need-human-input.md",
            id: "20250101-120000-need-human-input",
            from: "overwatch",
            subject: "Need human input",
            body: "Please decide how to proceed.",
            status: DecisionStatus.Resolved);

        Should.Throw<InvalidOperationException>(() => decision.Resolve("human", "Proceed.", DateTime.UtcNow));
    }

    [Fact]
    public void Resolve_WhenResponseMissing_ThrowsArgumentException()
    {
        var decision = CreateDecision();

        Should.Throw<ArgumentException>(() => decision.Resolve("human", " ", DateTime.UtcNow));
    }
}
