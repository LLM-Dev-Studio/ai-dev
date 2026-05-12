using Microsoft.Extensions.Logging.Abstractions;

namespace AiDevNet.Tests.Unit;

public class AgentServiceTests
{
    [Fact]
    public void CreateAgent_WhenTemplateMissing_ReturnsError()
    {
        var service = CreateService(out _);

        var result = service.CreateAgent(new ProjectSlug("demo-project"), "backend-dev", "Backend Dev", "missing-template");

        result.ShouldBeOfType<Err<AiDev.Models.Unit>>();
    }

    [Fact]
    public void SaveAgentMeta_WhenAgentMissing_ReturnsError()
    {
        var service = CreateService(out _);

        var result = service.SaveAgentMeta(new ProjectSlug("demo-project"), new AgentSlug("backend-dev"), "Backend Dev", "Builds APIs", "sonnet", AgentExecutorName.Default);

        result.ShouldBeOfType<Err<AiDev.Models.Unit>>();
    }

    [Fact]
    public void CreateAgent_WhenTemplateExists_ReturnsOk()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("demo-project");
        var templateJsonPath = paths.SafeTemplatePath("generic-standard", ".json")!;
        var templateMdPath = paths.SafeTemplatePath("generic-standard", ".md")!;
        Directory.CreateDirectory(Path.GetDirectoryName(templateJsonPath.Value)!);
        File.WriteAllText(templateJsonPath.Value, "{\"slug\":\"generic-standard\",\"name\":\"Generic\",\"role\":\"Implement features\",\"model\":\"sonnet\",\"description\":\"Generalist\",\"content\":\"\"}");
        File.WriteAllText(templateMdPath.Value, "# Generic\n\nYou build features.");

        var result = service.CreateAgent(projectSlug, "backend-dev", "Backend Dev", "generic-standard");

        result.ShouldBeOfType<Ok<AiDev.Models.Unit>>();
        paths.AgentJsonPath(projectSlug, new AgentSlug("backend-dev")).Exists().ShouldBeTrue();
    }

    [Fact]
    public void LoadAgent_WhenJsonContainsLastError_LoadsPersistedFailureState()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("demo-project");
        var agentSlug = new AgentSlug("backend-dev");
        var agentDir = paths.AgentDir(projectSlug, agentSlug);
        Directory.CreateDirectory(agentDir.Value);
        var errorAt = DateTime.UtcNow;

        File.WriteAllText(paths.AgentJsonPath(projectSlug, agentSlug).Value, $$"""
        {
          "slug": "backend-dev",
          "name": "Backend Dev",
          "role": "Implementer",
          "description": "Handles backend work",
          "model": "gemma3:27b",
          "status": "error",
          "lastError": "Ollama model 'gemma3:27b' does not support workspace tools.",
          "lastErrorAt": "{{errorAt:o}}",
          "skills": ["mcp-workspace"]
        }
        """);

        var agent = service.LoadAgent(projectSlug, agentSlug);

        var failed = agent.ShouldBeOfType<AgentInfoFailed>();
        failed.Failure.Error.ShouldContain("does not support workspace tools");
        failed.Failure.OccurredAt.ShouldBe(errorAt);
    }

    [Fact]
    public void CreateAgent_WhenTemplateHasCompactContent_WritesCompactClaudeMd()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("demo-project");
        var templateJsonPath = paths.SafeTemplatePath("generic-standard", ".json")!;
        var templateMdPath = paths.SafeTemplatePath("generic-standard", ".md")!;
        var templateCompactPath = paths.SafeTemplatePath("generic-standard", ".compact.md")!;
        Directory.CreateDirectory(Path.GetDirectoryName(templateJsonPath.Value)!);
        File.WriteAllText(templateJsonPath.Value, "{\"slug\":\"generic-standard\",\"name\":\"Generic\",\"role\":\"Implement features\",\"model\":\"sonnet\",\"description\":\"Generalist\",\"content\":\"\"}");
        File.WriteAllText(templateMdPath.Value, "# Generic\n\nFull content.");
        File.WriteAllText(templateCompactPath.Value, "# Generic\n\nCompact content.");

        service.CreateAgent(projectSlug, "backend-dev", "Backend Dev", "generic-standard");

        var agentSlug = new AgentSlug("backend-dev");
        var compactPath = Path.Combine(paths.AgentDir(projectSlug, agentSlug).Value, "CLAUDE.compact.md");
        File.Exists(compactPath).ShouldBeTrue();
        File.ReadAllText(compactPath).ShouldBe("# Generic\n\nCompact content.");
    }

    [Fact]
    public void CreateAgent_WhenTemplateHasNoCompactContent_DoesNotWriteCompactClaudeMd()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("demo-project");
        var templateJsonPath = paths.SafeTemplatePath("generic-standard", ".json")!;
        var templateMdPath = paths.SafeTemplatePath("generic-standard", ".md")!;
        Directory.CreateDirectory(Path.GetDirectoryName(templateJsonPath.Value)!);
        File.WriteAllText(templateJsonPath.Value, "{\"slug\":\"generic-standard\",\"name\":\"Generic\",\"role\":\"Implement features\",\"model\":\"sonnet\",\"description\":\"Generalist\",\"content\":\"\"}");
        File.WriteAllText(templateMdPath.Value, "# Generic\n\nFull content.");

        service.CreateAgent(projectSlug, "backend-dev", "Backend Dev", "generic-standard");

        var agentSlug = new AgentSlug("backend-dev");
        var compactPath = Path.Combine(paths.AgentDir(projectSlug, agentSlug).Value, "CLAUDE.compact.md");
        File.Exists(compactPath).ShouldBeFalse();
    }

    private static AgentService CreateService(out WorkspacePaths paths)
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        paths = new WorkspacePaths(root);
        var modelRegistry = Substitute.For<IModelRegistry>();
        modelRegistry.Find(Arg.Any<AgentExecutorName>(), Arg.Any<string>()).Returns((ModelDescriptor?)null);
        modelRegistry.GetModelsForExecutor(Arg.Any<AgentExecutorName>()).Returns([]);
        return new AgentService(
            paths,
            new AgentTemplatesService(paths),
            new AtomicFileWriter(),
            new ProjectMutationCoordinator(),
            modelRegistry,
            NullLogger<AgentService>.Instance);
    }
}
