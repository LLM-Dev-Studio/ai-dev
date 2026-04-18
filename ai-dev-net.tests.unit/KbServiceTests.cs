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

    [Fact]
    public void Create_WhenSlugMissing_ReturnsError()
    {
        var service = CreateService(out _);

        var result = service.Create(new ProjectSlug("demo-project"), "  ");

        result.ShouldBeOfType<Err<AiDev.Models.Unit>>();
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

        var result = service.Create(new ProjectSlug("demo-project"), "path/to/article");

        result.ShouldBeOfType<Err<AiDev.Models.Unit>>();
    }

    [Fact]
    public void Create_WhenSlugContainsBackslash_ReturnsError()
    {
        var service = CreateService(out _);

        var result = service.Create(new ProjectSlug("demo-project"), "path\\to\\article");

        result.ShouldBeOfType<Err<AiDev.Models.Unit>>();
    }

    [Fact]
    public void Create_WhenArticleAlreadyExists_ReturnsError()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("demo-project");

        service.Create(projectSlug, "deployment");
        var result = service.Create(projectSlug, "deployment");

        result.ShouldBeOfType<Err<AiDev.Models.Unit>>();
    }

    [Fact]
    public void ListArticles_WhenDirectoryDoesNotExist_ReturnsEmptyList()
    {
        var service = CreateService(out _);

        var result = service.ListArticles(new ProjectSlug("demo-project"));

        result.ShouldBeEmpty();
    }

    [Fact]
    public void ListArticles_WhenArticlesExist_ReturnsSortedByTitle()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("demo-project");

        service.Create(projectSlug, "zebra");
        service.Create(projectSlug, "alpha");
        service.Create(projectSlug, "beta");

        var result = service.ListArticles(projectSlug);

        result.Count.ShouldBe(3);
        result[0].Title.ShouldBe("alpha");
        result[1].Title.ShouldBe("beta");
        result[2].Title.ShouldBe("zebra");
    }

    [Fact]
    public void ListArticles_ExtractsTitleFromContent()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("demo-project");

        service.Save(projectSlug, "deploy", "# Deployment Guide\n\nSteps");

        var result = service.ListArticles(projectSlug);

        result[0].Title.ShouldBe("Deployment Guide");
    }

    [Fact]
    public void ListArticles_UsesSlugWhenNoTitleFound()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("demo-project");

        service.Save(projectSlug, "deploy", "No heading\n\nContent");

        var result = service.ListArticles(projectSlug);

        result[0].Title.ShouldBe("deploy");
    }

    [Fact]
    public void GetContent_WhenArticleDoesNotExist_ReturnsEmptyString()
    {
        var service = CreateService(out _);

        var result = service.GetContent(new ProjectSlug("demo-project"), "nonexistent");

        result.ShouldBeEmpty();
    }

    [Fact]
    public void GetContent_WhenArticleExists_ReturnsFullContent()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("demo-project");
        var content = "# Deployment\n\nFull content";

        service.Save(projectSlug, "deploy", content);

        var result = service.GetContent(projectSlug, "deploy");

        result.ShouldBe(content);
    }

    [Fact]
    public void Delete_WhenArticleExists_RemovesFile()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("demo-project");

        service.Create(projectSlug, "deploy");
        service.Delete(projectSlug, "deploy");

        var result = service.ListArticles(projectSlug);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Delete_WhenArticleDoesNotExist_IsNoOp()
    {
        var service = CreateService(out _);

        // Should not throw
        service.Delete(new ProjectSlug("demo-project"), "nonexistent");
    }

    [Fact]
    public void BuildInjectionContext_WhenKbDirectoryDoesNotExist_ReturnsNull()
    {
        var service = CreateService(out _);

        var result = service.BuildInjectionContext(new ProjectSlug("demo-project"), "any text");

        result.ShouldBeNull();
    }

    [Fact]
    public void BuildInjectionContext_WhenNoArticlesExist_ReturnsNull()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("demo-project");
        Directory.CreateDirectory(paths.KbDir(projectSlug));

        var result = service.BuildInjectionContext(projectSlug, "any text");

        result.ShouldBeNull();
    }

    [Fact]
    public void BuildInjectionContext_WhenArticlesExist_IncludesArticlesWithoutTrigger()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("demo-project");

        service.Save(projectSlug, "general", "# General Knowledge\n\nAlways needed");

        var result = service.BuildInjectionContext(projectSlug, "");

        result.ShouldNotBeNull();
        result.ShouldContain("General Knowledge");
        result.ShouldContain("Always needed");
    }

    [Fact]
    public void BuildInjectionContext_WhenArticleHasTriggerAndMatches_IncludesArticle()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("demo-project");

        var content = "---\ntrigger: database connection\n---\n# Database Help\n\nConnection info";
        service.Save(projectSlug, "db", content);

        var result = service.BuildInjectionContext(projectSlug, "I need help with database issues");

        result.ShouldNotBeNull();
        result.ShouldContain("Database Help");
    }

    [Fact]
    public void BuildInjectionContext_WhenArticleHasTriggerButDoesNotMatch_ExcludesArticle()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("demo-project");

        var content = "---\ntrigger: database connection\n---\n# Database Help\n\nConnection info";
        service.Save(projectSlug, "db", content);

        var result = service.BuildInjectionContext(projectSlug, "How do I deploy code?");

        result.ShouldBeNull();
    }

    [Fact]
    public void BuildInjectionContext_WithMultipleTriggerWords_MatchesAny()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("demo-project");

        var content = "---\ntrigger: database connection pool\n---\n# DB Help\n\nContent";
        service.Save(projectSlug, "db", content);

        var result = service.BuildInjectionContext(projectSlug, "I have a pool issue");

        result.ShouldNotBeNull();
        result.ShouldContain("DB Help");
    }

    [Fact]
    public void BuildInjectionContext_TriggerMatchingIsCaseInsensitive()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("demo-project");

        var content = "---\ntrigger: DATABASE\n---\n# DB Help\n\nContent";
        service.Save(projectSlug, "db", content);

        var result = service.BuildInjectionContext(projectSlug, "help with database");

        result.ShouldNotBeNull();
    }

    [Fact]
    public void BuildInjectionContext_WhenArticleHasFrontmatter_ExtractsBodyOnly()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("demo-project");

        var content = "---\ntrigger: test\n---\n# Article\n\nBody content";
        service.Save(projectSlug, "test", content);

        var result = service.BuildInjectionContext(projectSlug, "test");

        result.ShouldNotBeNull();
        result.ShouldContain("Body content");
        result.ShouldNotContain("trigger:");
    }

    [Fact]
    public void ListArticles_IncludesTriggerField()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("demo-project");

        var content = "---\ntrigger: database\n---\n# DB\n\nContent";
        service.Save(projectSlug, "db", content);

        var result = service.ListArticles(projectSlug);

        result[0].Trigger.ShouldBe("database");
    }

    [Fact]
    public void ListArticles_WithoutTriggerField_HasNullTrigger()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("demo-project");

        service.Create(projectSlug, "general");

        var result = service.ListArticles(projectSlug);

        result[0].Trigger.ShouldBeNull();
    }

    private static KbService CreateService(out WorkspacePaths paths)
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        paths = new WorkspacePaths(root);
        return new KbService(paths, new AtomicFileWriter(), new ProjectMutationCoordinator());
    }
}
