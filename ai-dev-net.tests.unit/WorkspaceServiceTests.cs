namespace AiDevNet.Tests.Unit;

public class WorkspaceServiceTests : IDisposable
{
    private readonly string _codebasePath;
    private readonly string _registryPath;
    private readonly WorkspaceService _service;

    public WorkspaceServiceTests()
    {
        _codebasePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_codebasePath);
        _registryPath = Path.Combine(_codebasePath, "test-managed-projects.json");
        var paths = new WorkspacePaths(new RootDir(Path.Combine(_codebasePath, ".ai-dev")));
        _service = new WorkspaceService(paths, new AtomicFileWriter(), registryFilePath: _registryPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_codebasePath))
            Directory.Delete(_codebasePath, recursive: true);
    }

    [Fact]
    public void CreateProject_WhenSlugInvalid_ReturnsError()
    {
        var result = _service.CreateProject(_codebasePath, "Invalid Slug", "Demo", null);

        result.ShouldBeOfType<Err<AiDev.Models.Unit>>();
    }

    [Fact]
    public void CreateProject_WhenValid_ReturnsOk()
    {
        var paths = new WorkspacePaths(new RootDir(Path.Combine(_codebasePath, ".ai-dev")));

        var result = _service.CreateProject(_codebasePath, "demo-project", "Demo Project", "Main app");

        result.ShouldBeOfType<Ok<AiDev.Models.Unit>>();
        paths.ProjectJsonPath(new ProjectSlug("demo-project")).Exists().ShouldBeTrue();
    }

    [Fact]
    public void CreateProject_WhenValid_StoresSlugInRegistry()
    {
        _service.CreateProject(_codebasePath, "demo-project", "Demo Project", null);

        var json = File.ReadAllText(_registryPath);
        json.ShouldContain("\"slug\"");
        json.ShouldContain("demo-project");
    }

    [Fact]
    public void UpdateProject_WhenMissing_ReturnsError()
    {
        var result = _service.UpdateProject(new ProjectSlug("demo-project"), "Demo Project", null);

        result.ShouldBeOfType<Err<AiDev.Models.Unit>>();
    }
}
