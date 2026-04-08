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

        var result = service.SaveAgentMeta(new ProjectSlug("demo-project"), new AgentSlug("backend-dev"), "Backend Dev", "Builds APIs", "sonnet", "default");

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

        agent.ShouldNotBeNull();
        agent!.Status.IsError.ShouldBeTrue();
        agent.LastError.ShouldContain("does not support workspace tools");
        agent.LastErrorAt.ShouldBe(errorAt);
    }

    private static AgentService CreateService(out WorkspacePaths paths)
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        paths = new WorkspacePaths(root);
        return new AgentService(paths, new StudioSettingsService(paths), new AgentTemplatesService(paths), new AtomicFileWriter(), new ProjectMutationCoordinator());
    }
}
