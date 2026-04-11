using AiDev.Mcp;

namespace AiDevNet.Tests.Unit;

public class PathValidatorTests
{
    [Fact]
    public void ResolveProjectRoot_ReturnsProjectDirectoryWithinWorkspace()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var validator = new PathValidator(workspaceRoot);

        var projectRoot = validator.ResolveProjectRoot("demo-project");

        projectRoot.ShouldBe(Path.Combine(workspaceRoot, "demo-project"));
    }

    [Fact]
    public void ResolveProject_WhenPathEscapesProject_ThrowsInvalidOperationException()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var validator = new PathValidator(workspaceRoot);

        Should.Throw<InvalidOperationException>(() => validator.ResolveProject("demo-project", Path.Combine("..", "other-project", "agent.json")));
    }

    [Fact]
    public void ValidateProjectAbsolute_WhenAbsolutePathLeavesProject_ThrowsInvalidOperationException()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var validator = new PathValidator(workspaceRoot);
        var escapedPath = Path.Combine(workspaceRoot, "other-project", "agent.json");

        Should.Throw<InvalidOperationException>(() => validator.ValidateProjectAbsolute("demo-project", escapedPath));
    }
}
