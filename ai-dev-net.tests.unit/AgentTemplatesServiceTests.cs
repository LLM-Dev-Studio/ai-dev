namespace AiDevNet.Tests.Unit;

public class AgentTemplatesServiceTests
{
    private static (AgentTemplatesService service, string templatesDir) CreateService()
    {
        var templatesDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        return (new AgentTemplatesService(templatesDir), templatesDir);
    }

    private static void WriteTemplate(string templatesDir, string slug, string mdContent, string? compactContent = null)
    {
        Directory.CreateDirectory(templatesDir);
        File.WriteAllText(Path.Combine(templatesDir, $"{slug}.json"),
            $"{{\"slug\":\"{slug}\",\"name\":\"Test\",\"role\":\"tester\",\"model\":\"sonnet\",\"description\":\"Test agent\",\"content\":\"\"}}");
        File.WriteAllText(Path.Combine(templatesDir, $"{slug}.md"), mdContent);
        if (compactContent is not null)
            File.WriteAllText(Path.Combine(templatesDir, $"{slug}.compact.md"), compactContent);
    }

    private static void WritePartial(string templatesDir, string partialKey, string content)
    {
        var sharedDir = Path.Combine(templatesDir, "shared");
        Directory.CreateDirectory(sharedDir);
        var fileName = partialKey.Replace("shared/", "") + ".md";
        File.WriteAllText(Path.Combine(sharedDir, fileName), content);
    }

    [Fact]
    public void GetTemplate_WithPartialIncludes_ComposesContentFromPartials()
    {
        var (service, templatesDir) = CreateService();
        WritePartial(templatesDir, "shared/tools", "## Tools\nUse MCP.");
        WriteTemplate(templatesDir, "developer-standard", "# Developer\n\n{{> shared/tools}}\n\n## Workflow");

        var template = service.GetTemplate("developer-standard");

        template.ShouldNotBeNull();
        template!.Content.ShouldContain("## Tools\nUse MCP.");
        template.Content.ShouldNotContain("{{> shared/tools}}");
    }

    [Fact]
    public void GetTemplate_WithCompactFile_PopulatesCompactContent()
    {
        var (service, templatesDir) = CreateService();
        WriteTemplate(templatesDir, "developer-standard",
            mdContent: "# Developer\n\nFull content.",
            compactContent: "# Developer\n\nCompact content.");

        var template = service.GetTemplate("developer-standard");

        template.ShouldNotBeNull();
        template!.CompactContent.ShouldBe("# Developer\n\nCompact content.");
    }

    [Fact]
    public void GetTemplate_WithoutCompactFile_CompactContentIsEmpty()
    {
        var (service, templatesDir) = CreateService();
        WriteTemplate(templatesDir, "developer-standard", mdContent: "# Developer\n\nFull content.");

        var template = service.GetTemplate("developer-standard");

        template.ShouldNotBeNull();
        template!.CompactContent.ShouldBe(string.Empty);
    }
}
