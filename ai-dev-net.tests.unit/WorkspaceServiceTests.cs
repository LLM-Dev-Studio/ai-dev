namespace AiDevNet.Tests.Unit;

public class WorkspaceServiceTests
{
    [Fact]
    public void CreateProject_WhenSlugInvalid_ReturnsError()
    {
        var (service, codebasePath) = CreateService();

        var result = service.CreateProject(codebasePath, "Invalid Slug", "Demo", null);

        result.ShouldBeOfType<Err<AiDev.Models.Unit>>();
    }

    [Fact]
    public void CreateProject_WhenValid_ReturnsOk()
    {
        var (service, codebasePath) = CreateService();
        var paths = new WorkspacePaths(new RootDir(Path.Combine(codebasePath, ".ai-dev")));

        var result = service.CreateProject(codebasePath, "demo-project", "Demo Project", "Main app");

        result.ShouldBeOfType<Ok<AiDev.Models.Unit>>();
        paths.ProjectJsonPath(new ProjectSlug("demo-project")).Exists().ShouldBeTrue();
    }

    [Fact]
    public void UpdateProject_WhenMissing_ReturnsError()
    {
        var (service, codebasePath) = CreateService();

        var result = service.UpdateProject(new ProjectSlug("demo-project"), "Demo Project", null);

        result.ShouldBeOfType<Err<AiDev.Models.Unit>>();
    }

    private static (WorkspaceService service, string codebasePath) CreateService()
    {
        var codebasePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(codebasePath);
        var holder = new ActiveWorkspaceHolder();
        holder.Activate(codebasePath);
        return (new WorkspaceService(holder, new AtomicFileWriter()), codebasePath);
    }
}
