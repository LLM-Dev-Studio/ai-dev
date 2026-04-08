using AiDev.Features.KnowledgeBase;

namespace AiDevNet.Tests.Unit;

public class KbServiceTests
{
    [Fact]
    public void Create_WhenSlugInvalid_ReturnsError()
    {
        var service = CreateService(out _);

        var result = service.Create(new ProjectSlug("demo-project"), "../bad");

        result.ShouldBeOfType<Err<AiDev.Models.Unit>>();
    }

    [Fact]
    public void Save_WhenValid_WritesArticle()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("demo-project");

        var result = service.Save(projectSlug, "deployment", "# Deployment\n\nSteps");

        result.ShouldBeOfType<Ok<AiDev.Models.Unit>>();
        File.Exists(Path.Combine(paths.KbDir(projectSlug), "deployment.md")).ShouldBeTrue();
    }

    private static KbService CreateService(out WorkspacePaths paths)
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        paths = new WorkspacePaths(root);
        return new KbService(paths, new AtomicFileWriter(), new ProjectMutationCoordinator());
    }
}
