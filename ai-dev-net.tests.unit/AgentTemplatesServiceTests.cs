namespace AiDevNet.Tests.Unit;

public class AgentTemplatesServiceTests
{
    private static (AgentTemplatesService service, WorkspacePaths paths) CreateService()
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var paths = new WorkspacePaths(root);
        return (new AgentTemplatesService(paths), paths);
    }

    private static void WriteTemplate(WorkspacePaths paths, string slug, string mdContent, string? compactContent = null)
    {
        var dir = paths.AgentTemplatesDir.Value;
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"{slug}.json"),
            $"{{\"slug\":\"{slug}\",\"name\":\"Test\",\"role\":\"tester\",\"model\":\"sonnet\",\"description\":\"Test agent\",\"content\":\"\"}}");
        File.WriteAllText(Path.Combine(dir, $"{slug}.md"), mdContent);
        if (compactContent is not null)
            File.WriteAllText(Path.Combine(dir, $"{slug}.compact.md"), compactContent);
    }

    private static void WritePartial(WorkspacePaths paths, string partialKey, string content)
    {
        var sharedDir = Path.Combine(paths.AgentTemplatesDir.Value, "shared");
        Directory.CreateDirectory(sharedDir);
        var fileName = partialKey.Replace("shared/", "") + ".md";
        File.WriteAllText(Path.Combine(sharedDir, fileName), content);
    }

    [Fact]
    public void GetTemplate_WithPartialIncludes_ComposesContentFromPartials()
    {
        var (service, paths) = CreateService();
        WritePartial(paths, "shared/tools", "## Tools\nUse MCP.");
        WriteTemplate(paths, "developer-standard", "# Developer\n\n{{> shared/tools}}\n\n## Workflow");

        var template = service.GetTemplate("developer-standard");

        template.ShouldNotBeNull();
        template!.Content.ShouldContain("## Tools\nUse MCP.");
        template.Content.ShouldNotContain("{{> shared/tools}}");
    }

    [Fact]
    public void GetTemplate_WithCompactFile_PopulatesCompactContent()
    {
        var (service, paths) = CreateService();
        WriteTemplate(paths, "developer-standard",
            mdContent: "# Developer\n\nFull content.",
            compactContent: "# Developer\n\nCompact content.");

        var template = service.GetTemplate("developer-standard");

        template.ShouldNotBeNull();
        template!.CompactContent.ShouldBe("# Developer\n\nCompact content.");
    }

    [Fact]
    public void GetTemplate_WithoutCompactFile_CompactContentIsEmpty()
    {
        var (service, paths) = CreateService();
        WriteTemplate(paths, "developer-standard", mdContent: "# Developer\n\nFull content.");

        var template = service.GetTemplate("developer-standard");

        template.ShouldNotBeNull();
        template!.CompactContent.ShouldBe(string.Empty);
    }
}
