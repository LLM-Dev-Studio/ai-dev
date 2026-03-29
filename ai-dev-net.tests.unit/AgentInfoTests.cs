namespace AiDevNet.Tests.Unit;

public class AgentInfoTests
{
    // -------------------------------------------------------------------------
    // Defaults
    // -------------------------------------------------------------------------

    [Fact]
    public void Defaults_AreCorrect()
    {
        var info = new AgentInfo { Slug = new AgentSlug("my-agent") };

        info.Name.ShouldBe(string.Empty);
        info.Role.ShouldBe(string.Empty);
        info.Model.ShouldBe("sonnet");
        info.Status.ShouldBe(AgentStatus.Idle);
        info.Description.ShouldBe(string.Empty);
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
        var info = new AgentInfo { Slug = new AgentSlug("my-agent"), Status = AgentStatus.Running };
        info.Status.IsRunning.ShouldBeTrue();
    }

    [Fact]
    public void Status_CanBeSetToError()
    {
        var info = new AgentInfo { Slug = new AgentSlug("my-agent"), Status = AgentStatus.Error };
        info.Status.IsError.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // LastRunAt
    // -------------------------------------------------------------------------

    [Fact]
    public void LastRunAt_WhenSet_ReturnsCorrectDateTime()
    {
        var now = DateTime.UtcNow;
        var info = new AgentInfo { Slug = new AgentSlug("my-agent"), LastRunAt = now };
        info.LastRunAt.ShouldBe(now);
    }

    // -------------------------------------------------------------------------
    // InboxCount
    // -------------------------------------------------------------------------

    [Fact]
    public void InboxCount_ReflectsAssignedValue()
    {
        var info = new AgentInfo { Slug = new AgentSlug("my-agent"), InboxCount = 5 };
        info.InboxCount.ShouldBe(5);
    }

    // -------------------------------------------------------------------------
    // Executor default
    // -------------------------------------------------------------------------

    [Fact]
    public void Executor_DefaultMatchesIAgentExecutorDefault()
    {
        var info = new AgentInfo { Slug = new AgentSlug("my-agent") };
        info.Executor.ShouldBe(IAgentExecutor.Default);
    }
}
