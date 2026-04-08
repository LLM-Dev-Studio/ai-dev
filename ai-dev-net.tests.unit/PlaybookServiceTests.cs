using AiDev.Features.Playbook;

namespace AiDevNet.Tests.Unit;

public class PlaybookServiceTests
{
    [Fact]
    public void Create_WhenSlugMissing_ReturnsError()
    {
        var service = CreateService(out _);

        var result = service.Create(new ProjectSlug("demo-project"), " ");

        result.ShouldBeOfType<Err<AiDev.Models.Unit>>();
    }

    [Fact]
    public void Save_WhenValid_WritesPlaybook()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("demo-project");

        var result = service.Save(projectSlug, "deploy-check", "# Deploy Check\n\nSteps");

        result.ShouldBeOfType<Ok<AiDev.Models.Unit>>();
        File.Exists(Path.Combine(paths.PlaybooksDir(projectSlug), "deploy-check.md")).ShouldBeTrue();
    }

    private static PlaybookService CreateService(out WorkspacePaths paths)
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        paths = new WorkspacePaths(root);
        return new PlaybookService(paths, new AtomicFileWriter(), new ProjectMutationCoordinator());
    }
}
