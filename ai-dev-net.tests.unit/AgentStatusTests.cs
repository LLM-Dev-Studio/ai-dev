namespace AiDevNet.Tests.Unit;

public class AgentStatusTests
{
    // -------------------------------------------------------------------------
    // Static instances
    // -------------------------------------------------------------------------

    [Fact]
    public void StaticInstances_HaveCorrectValues()
    {
        AgentStatus.Idle.Value.ShouldBe("idle");
        AgentStatus.Running.Value.ShouldBe("running");
        AgentStatus.Error.Value.ShouldBe("error");
    }

    // -------------------------------------------------------------------------
    // From()
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("running", "running")]
    [InlineData("RUNNING", "running")]   // case-insensitive
    [InlineData("error", "error")]
    [InlineData("ERROR", "error")]
    [InlineData("idle", "idle")]
    [InlineData(null, "idle")]      // null → idle
    [InlineData("", "idle")]      // empty → idle
    [InlineData("unknown", "idle")]      // unknown → idle
    public void From_MapsToExpectedStatus(string? input, string expectedValue)
    {
        AgentStatus.From(input).Value.ShouldBe(expectedValue);
    }

    // -------------------------------------------------------------------------
    // Boolean helpers
    // -------------------------------------------------------------------------

    [Fact]
    public void IsIdle_TrueOnlyForIdle()
    {
        AgentStatus.Idle.IsIdle.ShouldBeTrue();
        AgentStatus.Running.IsIdle.ShouldBeFalse();
        AgentStatus.Error.IsIdle.ShouldBeFalse();
    }

    [Fact]
    public void IsRunning_TrueOnlyForRunning()
    {
        AgentStatus.Running.IsRunning.ShouldBeTrue();
        AgentStatus.Idle.IsRunning.ShouldBeFalse();
        AgentStatus.Error.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public void IsError_TrueOnlyForError()
    {
        AgentStatus.Error.IsError.ShouldBeTrue();
        AgentStatus.Idle.IsError.ShouldBeFalse();
        AgentStatus.Running.IsError.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // BadgeClasses
    // -------------------------------------------------------------------------

    [Fact]
    public void BadgeClasses_Running_HasPulseAnimation()
    {
        var (dot, text) = AgentStatus.Running.BadgeClasses;
        dot.ShouldContain("animate-pulse");
        text.ShouldContain("emerald");
    }

    [Fact]
    public void BadgeClasses_Error_HasRedClasses()
    {
        var (dot, text) = AgentStatus.Error.BadgeClasses;
        dot.ShouldContain("red");
        text.ShouldContain("red");
    }

    [Fact]
    public void BadgeClasses_Idle_HasMutedClasses()
    {
        var (dot, text) = AgentStatus.Idle.BadgeClasses;
        dot.ShouldContain("zinc");
        text.ShouldContain("zinc");
    }

    // -------------------------------------------------------------------------
    // Equality & ToString
    // -------------------------------------------------------------------------

    [Fact]
    public void ToString_ReturnsValue()
    {
        AgentStatus.Running.ToString().ShouldBe("running");
        AgentStatus.Idle.ToString().ShouldBe("idle");
    }

    [Fact]
    public void Equality_SameStatus_AreEqual()
    {
        AgentStatus.From("running").ShouldBe(AgentStatus.Running);
    }
}
