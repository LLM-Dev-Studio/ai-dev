namespace AiDevNet.Tests.Unit;

public class RunningSessionTests
{
    [Fact]
    public void Properties_RetainAssignedValues()
    {
        var project = new ProjectSlug("my-project");
        var agent = new AgentSlug("my-agent");
        var started = DateTime.UtcNow;

        var session = new RunningSession
        {
            ProjectSlug = project,
            AgentSlug = agent,
            StartedAt = started,
            Pid = 1234,
        };

        session.ProjectSlug.ShouldBe(project);
        session.AgentSlug.ShouldBe(agent);
        session.StartedAt.ShouldBe(started);
        session.Pid.ShouldBe(1234);
    }

    [Fact]
    public void Pid_DefaultsToZero()
    {
        var session = new RunningSession
        {
            ProjectSlug = new ProjectSlug("my-project"),
            AgentSlug = new AgentSlug("my-agent"),
            StartedAt = DateTime.UtcNow,
        };

        session.Pid.ShouldBe(0);
    }

    [Fact]
    public void Pid_IsMutable()
    {
        var session = new RunningSession
        {
            ProjectSlug = new ProjectSlug("my-project"),
            AgentSlug = new AgentSlug("my-agent"),
            StartedAt = DateTime.UtcNow,
        };

        session.Pid = 5678;
        session.Pid.ShouldBe(5678);
    }
}
