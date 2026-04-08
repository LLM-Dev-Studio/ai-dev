namespace AiDevNet.Tests.Unit;

public class WorkspaceProjectTests
{
    [Fact]
    public void Constructor_WhenNameMissing_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new WorkspaceProject(
            slug: new ProjectSlug("demo-project"),
            name: " "));
    }

    [Fact]
    public void Constructor_WhenAgentCountNegative_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new WorkspaceProject(
            slug: new ProjectSlug("demo-project"),
            name: "Demo Project",
            agentCount: -1));
    }

    [Fact]
    public void Constructor_NormalizesBlankDescriptionToNull()
    {
        var project = new WorkspaceProject(
            slug: new ProjectSlug("demo-project"),
            name: "Demo Project",
            description: " ");

        project.Description.ShouldBeNull();
    }

    [Fact]
    public void Constructor_PreservesAssignedValues()
    {
        var createdAt = DateTime.UtcNow;
        var project = new WorkspaceProject(
            slug: new ProjectSlug("demo-project"),
            name: "Demo Project",
            description: "Main application",
            createdAt: createdAt,
            agentCount: 3);

        project.Slug.Value.ShouldBe("demo-project");
        project.Name.ShouldBe("Demo Project");
        project.Description.ShouldBe("Main application");
        project.CreatedAt.ShouldBe(createdAt);
        project.AgentCount.ShouldBe(3);
    }
}
