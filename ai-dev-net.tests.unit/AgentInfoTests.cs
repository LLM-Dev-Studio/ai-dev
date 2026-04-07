namespace AiDevNet.Tests.Unit;

public class AgentInfoTests
{
    private static AgentInfo CreateInfo() => new(
        slug: new AgentSlug("my-agent"),
        name: "My Agent",
        role: "Assistant",
        description: "Handles agent workflows");

    // -------------------------------------------------------------------------
    // Defaults
    // -------------------------------------------------------------------------

    [Fact]
    public void Defaults_AreCorrect()
    {
        var info = CreateInfo();

        info.Name.ShouldBe("My Agent");
        info.Role.ShouldBe("Assistant");
        info.Model.ShouldBe("sonnet");
        info.Status.ShouldBe(AgentStatus.Idle);
        info.Description.ShouldBe("Handles agent workflows");
        info.LastRunAt.ShouldBeNull();
        info.InboxCount.ShouldBe(0);
        info.Executor.ShouldBe(IAgentExecutor.Default);
    }

    // -------------------------------------------------------------------------
    // Status
    // -------------------------------------------------------------------------

    [Fact]
    public void Status_CanBeSetToRunning()
    {
        var info = CreateInfo();
        info.MarkRunning(DateTime.UtcNow);
        info.Status.IsRunning.ShouldBeTrue();
    }

    [Fact]
    public void Status_CanBeSetToError()
    {
        var info = CreateInfo();
        info.MarkError();
        info.Status.IsError.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // LastRunAt
    // -------------------------------------------------------------------------

    [Fact]
    public void LastRunAt_WhenSet_ReturnsCorrectDateTime()
    {
        var now = DateTime.UtcNow;
        var info = CreateInfo();
        info.MarkRunning(now);
        info.LastRunAt.ShouldBe(now);
    }

    // -------------------------------------------------------------------------
    // InboxCount
    // -------------------------------------------------------------------------

    [Fact]
    public void InboxCount_ReflectsAssignedValue()
    {
        var info = CreateInfo();
        info.SetInboxCount(5);
        info.InboxCount.ShouldBe(5);
    }

    // -------------------------------------------------------------------------
    // Executor default
    // -------------------------------------------------------------------------

    [Fact]
    public void Executor_DefaultMatchesIAgentExecutorDefault()
    {
        var info = CreateInfo();
        info.Executor.ShouldBe(IAgentExecutor.Default);
    }

    [Fact]
    public void Constructor_WhenNameMissing_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new AgentInfo(
            slug: new AgentSlug("my-agent"),
            name: " ",
            role: "Assistant",
            description: "Handles agent workflows"));
    }

    [Fact]
    public void SetInboxCount_WhenNegative_ThrowsArgumentOutOfRangeException()
    {
        var info = CreateInfo();

        Should.Throw<ArgumentOutOfRangeException>(() => info.SetInboxCount(-1));
    }
}
