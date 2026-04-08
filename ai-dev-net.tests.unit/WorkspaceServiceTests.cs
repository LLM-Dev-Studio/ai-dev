namespace AiDevNet.Tests.Unit;

public class WorkspaceServiceTests
{
    [Fact]
    public void CreateProject_WhenSlugInvalid_ReturnsError()
    {
        var service = CreateService(out _);

        var result = service.CreateProject("Invalid Slug", "Demo", null);

        result.ShouldBeOfType<Err<AiDev.Models.Unit>>();
    }

    [Fact]
    public void CreateProject_WhenValid_ReturnsOk()
    {
        var service = CreateService(out var paths);

        var result = service.CreateProject("demo-project", "Demo Project", "Main app");

        result.ShouldBeOfType<Ok<AiDev.Models.Unit>>();
        File.Exists(paths.ProjectJsonPath(new ProjectSlug("demo-project"))).ShouldBeTrue();
    }

    [Fact]
    public void UpdateProject_WhenMissing_ReturnsError()
    {
        var service = CreateService(out _);

        var result = service.UpdateProject(new ProjectSlug("demo-project"), "Demo Project", null, null);

        result.ShouldBeOfType<Err<AiDev.Models.Unit>>();
    }

    private static WorkspaceService CreateService(out WorkspacePaths paths)
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        paths = new WorkspacePaths(root);
        return new WorkspaceService(paths, new AtomicFileWriter());
    }
}
