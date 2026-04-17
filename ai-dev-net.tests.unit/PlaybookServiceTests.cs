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

    [Fact]
    public void Create_WhenSlugContainsDotDot_ReturnsError()
    {
        var service = CreateService(out _);

        var result = service.Create(new ProjectSlug("demo-project"), "../escaped");

        result.ShouldBeOfType<Err<AiDev.Models.Unit>>();
    }

    [Fact]
    public void Create_WhenSlugContainsForwardSlash_ReturnsError()
    {
        var service = CreateService(out _);

        var result = service.Create(new ProjectSlug("demo-project"), "path/to/playbook");

        result.ShouldBeOfType<Err<AiDev.Models.Unit>>();
    }

    [Fact]
    public void Create_WhenSlugContainsBackslash_ReturnsError()
    {
        var service = CreateService(out _);

        var result = service.Create(new ProjectSlug("demo-project"), "path\\to\\playbook");

        result.ShouldBeOfType<Err<AiDev.Models.Unit>>();
    }

    [Fact]
    public void Create_WhenSlugAlreadyExists_ReturnsError()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("demo-project");

        service.Create(projectSlug, "deploy");
        var result = service.Create(projectSlug, "deploy");

        result.ShouldBeOfType<Err<AiDev.Models.Unit>>();
    }

    [Fact]
    public void ListPlaybooks_WhenDirectoryDoesNotExist_ReturnsEmptyList()
    {
        var service = CreateService(out _);

        var result = service.ListPlaybooks(new ProjectSlug("demo-project"));

        result.ShouldBeEmpty();
    }

    [Fact]
    public void ListPlaybooks_WhenPlaybooksExist_ReturnsSortedByTitle()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("demo-project");

        service.Create(projectSlug, "zebra-playbook");
        service.Create(projectSlug, "alpha-playbook");
        service.Create(projectSlug, "beta-playbook");

        var result = service.ListPlaybooks(projectSlug);

        result.Count.ShouldBe(3);
        result[0].Title.ShouldBe("alpha-playbook");
        result[1].Title.ShouldBe("beta-playbook");
        result[2].Title.ShouldBe("zebra-playbook");
    }

    [Fact]
    public void ListPlaybooks_ExtractsTitleFromContent()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("demo-project");

        service.Save(projectSlug, "deploy", "# Deploy Process\n\nSteps here");

        var result = service.ListPlaybooks(projectSlug);

        result.Count.ShouldBe(1);
        result[0].Title.ShouldBe("Deploy Process");
    }

    [Fact]
    public void ListPlaybooks_UsesSlugWhenNoTitleFound()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("demo-project");

        service.Save(projectSlug, "deploy", "No heading\n\nJust content");

        var result = service.ListPlaybooks(projectSlug);

        result[0].Title.ShouldBe("deploy");
    }

    [Fact]
    public void GetInjectionContext_WhenPlaybookDoesNotExist_ReturnsNull()
    {
        var service = CreateService(out _);

        var result = service.GetInjectionContext(new ProjectSlug("demo-project"), "nonexistent");

        result.ShouldBeNull();
    }

    [Fact]
    public void GetInjectionContext_WhenPlaybookExists_ReturnsFormattedContent()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("demo-project");

        service.Save(projectSlug, "deploy", "# Deploy\n\nStep 1\nStep 2");

        var result = service.GetInjectionContext(projectSlug, "deploy");

        result.ShouldStartWith("## Playbook: Deploy");
        result.ShouldContain("Step 1");
    }

    [Fact]
    public void GetInjectionContext_WithFrontmatter_ExtractsBody()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("demo-project");
        var content = "---\nmacro: deploy-macro\n---\n# Deploy\n\nBody content";

        service.Save(projectSlug, "deploy", content);

        var result = service.GetInjectionContext(projectSlug, "deploy");

        result.ShouldContain("Body content");
        result.ShouldNotContain("macro: deploy-macro");
    }

    [Fact]
    public void GetContent_WhenPlaybookDoesNotExist_ReturnsEmptyString()
    {
        var service = CreateService(out _);

        var result = service.GetContent(new ProjectSlug("demo-project"), "nonexistent");

        result.ShouldBeEmpty();
    }

    [Fact]
    public void GetContent_WhenPlaybookExists_ReturnsFullContent()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("demo-project");
        var content = "# Deploy\n\nFull content here";

        service.Save(projectSlug, "deploy", content);

        var result = service.GetContent(projectSlug, "deploy");

        result.ShouldBe(content);
    }

    [Fact]
    public void Delete_WhenPlaybookExists_RemovesFile()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("demo-project");

        service.Create(projectSlug, "deploy");
        service.Delete(projectSlug, "deploy");

        var result = service.ListPlaybooks(projectSlug);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Delete_WhenPlaybookDoesNotExist_IsNoOp()
    {
        var service = CreateService(out _);

        // Should not throw
        service.Delete(new ProjectSlug("demo-project"), "nonexistent");
    }

    [Fact]
    public void Delete_WhenSlugIsInvalid_IsNoOp()
    {
        var service = CreateService(out _);

        // Should not throw
        service.Delete(new ProjectSlug("demo-project"), "../escaped");
    }

    [Fact]
    public void ListPlaybooks_IncludesMacroField()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("demo-project");
        var content = "---\nmacro: deploy-macro\n---\n# Deploy\n\nContent";

        service.Save(projectSlug, "deploy", content);

        var result = service.ListPlaybooks(projectSlug);

        result[0].Macro.ShouldBe("deploy-macro");
    }

    [Fact]
    public void ListPlaybooks_WithoutMacroField_HasNullMacro()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("demo-project");

        service.Create(projectSlug, "deploy");

        var result = service.ListPlaybooks(projectSlug);

        result[0].Macro.ShouldBeNull();
    }

    private static PlaybookService CreateService(out WorkspacePaths paths)
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        paths = new WorkspacePaths(root);
        return new PlaybookService(paths, new AtomicFileWriter(), new ProjectMutationCoordinator());
    }
}
