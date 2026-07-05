namespace AiDevNet.Tests.Unit;

public class AgentInfoTests
{
    private static AgentInfoIdle CreateIdle(DateTime? previousRunAt = null) => new()
    {
        Slug          = new AgentSlug("my-agent"),
        Name          = "My Agent",
        Role          = "Assistant",
        Description   = "Handles agent workflows",
        Model         = "sonnet",
        Executor      = AgentExecutorName.Default,
        Skills        = [],
        ThinkingLevel = default,
        InboxCount    = 0,
        PreviousRunAt = previousRunAt,
    };

    // ── AgentInfoIdle ────────────────────────────────────────────────────────

    [Fact]
    public void AgentInfoIdle_DefaultProperties_AreCorrect()
    {
        var info = CreateIdle();

        info.Name.ShouldBe("My Agent");
        info.Role.ShouldBe("Assistant");
        info.Model.ShouldBe("sonnet");
        info.Description.ShouldBe("Handles agent workflows");
        info.InboxCount.ShouldBe(0);
        info.Executor.ShouldBe(AgentExecutorName.Claude);
        info.PreviousRunAt.ShouldBeNull();
    }

    [Fact]
    public void AgentInfoIdle_Status_IsIdle()
    {
        var info = CreateIdle();
        info.Status.ShouldBe(AgentStatus.Idle);
    }

    [Fact]
    public void AgentInfoIdle_LastRunAt_ReturnsPreviousRunAt()
    {
        var now  = DateTime.UtcNow;
        var info = CreateIdle(previousRunAt: now);
        info.LastRunAt.ShouldBe(now);
    }

    [Fact]
    public void AgentInfoIdle_LastError_IsNull()
    {
        var info = CreateIdle();
        info.LastError.ShouldBeNull();
        info.LastErrorAt.ShouldBeNull();
    }

    // ── AgentInfoRunning ─────────────────────────────────────────────────────

    [Fact]
    public void AgentInfoRunning_Status_IsRunning()
    {
        var info = CreateIdle() with { } as AgentInfo;
        var running = new AgentInfoRunning
        {
            Slug = new AgentSlug("my-agent"), Name = "My Agent", Role = "Assistant",
            Description = "Handles agent workflows", Model = "sonnet",
            Executor = AgentExecutorName.Default, Skills = [], ThinkingLevel = default,
            InboxCount = 0, StartedAt = DateTime.UtcNow,
        };

        running.Status.IsRunning.ShouldBeTrue();
    }

    [Fact]
    public void AgentInfoRunning_LastRunAt_ReturnsStartedAt()
    {
        var now     = DateTime.UtcNow;
        var running = new AgentInfoRunning
        {
            Slug = new AgentSlug("my-agent"), Name = "My Agent", Role = "Assistant",
            Description = "", Model = "sonnet", Executor = AgentExecutorName.Default,
            Skills = [], ThinkingLevel = default, InboxCount = 0, StartedAt = now,
        };

        running.LastRunAt.ShouldBe(now);
        running.StartedAt.ShouldBe(now);
    }

    // ── AgentInfoFailed ──────────────────────────────────────────────────────

    [Fact]
    public void AgentInfoFailed_Status_IsError()
    {
        var failed = new AgentInfoFailed
        {
            Slug = new AgentSlug("my-agent"), Name = "My Agent", Role = "Assistant",
            Description = "", Model = "sonnet", Executor = AgentExecutorName.Default,
            Skills = [], ThinkingLevel = default, InboxCount = 0,
            Failure = new AgentFailure("Something broke", DateTime.UtcNow),
        };

        failed.Status.IsError.ShouldBeTrue();
    }

    [Fact]
    public void AgentInfoFailed_FailureAlwaysPresent()
    {
        var occurredAt = DateTime.UtcNow;
        var failed = new AgentInfoFailed
        {
            Slug = new AgentSlug("my-agent"), Name = "My Agent", Role = "Assistant",
            Description = "", Model = "sonnet", Executor = AgentExecutorName.Default,
            Skills = [], ThinkingLevel = default, InboxCount = 0,
            Failure = new AgentFailure("Unsupported Ollama tools", occurredAt),
        };

        failed.Failure.Error.ShouldBe("Unsupported Ollama tools");
        failed.Failure.OccurredAt.ShouldBe(occurredAt);
        failed.LastError.ShouldBe("Unsupported Ollama tools");
        failed.LastErrorAt.ShouldBe(occurredAt);
    }

    // ── with expressions ─────────────────────────────────────────────────────

    [Fact]
    public void WithExpression_CanUpdateInboxCount()
    {
        var info    = CreateIdle();
        var updated = info with { InboxCount = 5 };

        updated.InboxCount.ShouldBe(5);
        info.InboxCount.ShouldBe(0); // original unchanged
    }

    [Fact]
    public void WithExpression_CanSetFailover()
    {
        var info    = CreateIdle();
        var failover = new AgentFailover(AgentExecutorName.Anthropic, DateTime.UtcNow);
        var updated  = info with { Failover = failover };

        updated.Failover.ShouldBe(failover);
        info.Failover.ShouldBeNull(); // original unchanged
    }
}
