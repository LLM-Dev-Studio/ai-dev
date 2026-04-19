using AiDev.Features.Digest;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiDevNet.Tests.Unit;

public class DigestServiceTests
{
    [Fact]
    public void GetDigest_WhenNoDirectoriesExist_ReturnsZeroCountsAndEmptyAgentList()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("test-project");

        var result = service.GetDigest(projectSlug, "2026-01-01");

        result.Date.ShouldBe("2026-01-01");
        result.TotalMessages.ShouldBe(0);
        result.PendingDecisions.ShouldBe(0);
        result.ResolvedDecisions.ShouldBe(0);
        result.AgentActivity.ShouldBeEmpty();
    }

    [Fact]
    public void GetDigest_WhenPendingDecisionsExist_CountsThemCorrectly()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var pendingDir = paths.DecisionsPendingDir(projectSlug);
        Directory.CreateDirectory(pendingDir);

        File.WriteAllText(Path.Combine(pendingDir, "decision1.md"), "content");
        File.WriteAllText(Path.Combine(pendingDir, "decision2.md"), "content");

        var result = service.GetDigest(projectSlug, "2026-01-01");

        result.PendingDecisions.ShouldBe(2);
    }

    [Fact]
    public void GetDigest_WhenResolvedDecisionsExistForDate_CountsThemCorrectly()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var resolvedDir = paths.DecisionsResolvedDir(projectSlug);
        Directory.CreateDirectory(resolvedDir);

        // Format: YYYYMMDD-HHMMSS-*.md — date prefix is first 8 chars
        File.WriteAllText(Path.Combine(resolvedDir, "20260101-120000-decision1.md"), "content");
        File.WriteAllText(Path.Combine(resolvedDir, "20260101-130000-decision2.md"), "content");
        File.WriteAllText(Path.Combine(resolvedDir, "20260102-120000-decision3.md"), "content");

        var result = service.GetDigest(projectSlug, "2026-01-01");

        result.ResolvedDecisions.ShouldBe(2);
    }

    [Fact]
    public void GetDigest_WhenAgentDirectoriesExist_IncludesAgentActivity()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentsDir = paths.AgentsDir(projectSlug);
        Directory.CreateDirectory(agentsDir);

        // Create agent directories
        var agent1Dir = Path.Combine(agentsDir, "agent1");
        Directory.CreateDirectory(agent1Dir);

        var result = service.GetDigest(projectSlug, "2026-01-01");

        result.AgentActivity.Count.ShouldBe(1);
        result.AgentActivity[0].AgentSlug.ShouldBe("agent1");
    }

    [Fact]
    public void GetDigest_WhenAgentJsonExists_LoadsAgentMetadata()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentSlug = new AgentSlug("agent1");
        var agentsDir = paths.AgentsDir(projectSlug);
        Directory.CreateDirectory(agentsDir);

        var agent1Dir = Path.Combine(agentsDir, "agent1");
        Directory.CreateDirectory(agent1Dir);

        var jsonPath = Path.Combine(agent1Dir, "agent.json");
        var jsonContent = @"{
            ""name"": ""Agent One"",
            ""executor"": ""anthropic"",
            ""model"": ""claude-3-opus""
        }";
        File.WriteAllText(jsonPath, jsonContent);

        var result = service.GetDigest(projectSlug, "2026-01-01");

        result.AgentActivity.Count.ShouldBe(1);
        result.AgentActivity[0].AgentName.ShouldBe("Agent One");
        result.AgentActivity[0].Executor.ShouldBe("anthropic");
        result.AgentActivity[0].Model.ShouldBe("claude-3-opus");
    }

    [Fact]
    public void GetDigest_WhenAgentJsonMissing_UsesDefaultName()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentsDir = paths.AgentsDir(projectSlug);
        Directory.CreateDirectory(agentsDir);

        var agent1Dir = Path.Combine(agentsDir, "agent1");
        Directory.CreateDirectory(agent1Dir);

        var result = service.GetDigest(projectSlug, "2026-01-01");

        result.AgentActivity[0].AgentName.ShouldBe("agent1");
        result.AgentActivity[0].Executor.ShouldBe(string.Empty);
        result.AgentActivity[0].Model.ShouldBe(string.Empty);
    }

    [Fact]
    public void GetDigest_WhenAgentJsonIsInvalid_IgnoresErrorAndUsesDefaults()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentsDir = paths.AgentsDir(projectSlug);
        Directory.CreateDirectory(agentsDir);

        var agent1Dir = Path.Combine(agentsDir, "agent1");
        Directory.CreateDirectory(agent1Dir);

        var jsonPath = Path.Combine(agent1Dir, "agent.json");
        File.WriteAllText(jsonPath, "{ invalid json");

        // Should not throw
        var result = service.GetDigest(projectSlug, "2026-01-01");

        result.AgentActivity[0].AgentName.ShouldBe("agent1");
    }

    [Fact]
    public void GetDigest_WhenInboxAndOutboxMessagesExist_CountsThemCorrectly()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentSlug = new AgentSlug("agent1");

        var agentsDir = paths.AgentsDir(projectSlug);
        Directory.CreateDirectory(agentsDir);
        var agent1Dir = Path.Combine(agentsDir, "agent1");
        Directory.CreateDirectory(agent1Dir);

        var inboxDir = paths.AgentInboxDir(projectSlug, agentSlug);
        var outboxDir = paths.AgentOutboxDir(projectSlug, agentSlug);
        Directory.CreateDirectory(inboxDir);
        Directory.CreateDirectory(outboxDir);

        // Create messages with date prefix 20260101
        File.WriteAllText(Path.Combine(inboxDir, "20260101-120000-msg1.md"), "content");
        File.WriteAllText(Path.Combine(inboxDir, "20260101-130000-msg2.md"), "content");
        File.WriteAllText(Path.Combine(outboxDir, "20260101-140000-msg1.md"), "content");

        var result = service.GetDigest(projectSlug, "2026-01-01");

        result.AgentActivity[0].MessagesReceived.ShouldBe(2);
        result.AgentActivity[0].MessagesSent.ShouldBe(1);
        result.TotalMessages.ShouldBe(2); // Only received messages count towards total
    }

    [Fact]
    public void GetDigest_WhenMessagesExistForDifferentDates_CountsOnlyForSpecificDate()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentSlug = new AgentSlug("agent1");

        var agentsDir = paths.AgentsDir(projectSlug);
        Directory.CreateDirectory(agentsDir);
        var agent1Dir = Path.Combine(agentsDir, "agent1");
        Directory.CreateDirectory(agent1Dir);

        var inboxDir = paths.AgentInboxDir(projectSlug, agentSlug);
        Directory.CreateDirectory(inboxDir);

        // Create messages for different dates
        File.WriteAllText(Path.Combine(inboxDir, "20260101-120000-msg1.md"), "content");
        File.WriteAllText(Path.Combine(inboxDir, "20260101-130000-msg2.md"), "content");
        File.WriteAllText(Path.Combine(inboxDir, "20260102-120000-msg3.md"), "content");

        var result = service.GetDigest(projectSlug, "2026-01-01");

        result.AgentActivity[0].MessagesReceived.ShouldBe(2);
    }

    [Fact]
    public void GetDigest_WhenMultipleAgentsExist_SortsAgentsByName()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentsDir = paths.AgentsDir(projectSlug);
        Directory.CreateDirectory(agentsDir);

        // Create agents in non-alphabetical order
        Directory.CreateDirectory(Path.Combine(agentsDir, "zebra-agent"));
        Directory.CreateDirectory(Path.Combine(agentsDir, "alpha-agent"));
        Directory.CreateDirectory(Path.Combine(agentsDir, "beta-agent"));

        var result = service.GetDigest(projectSlug, "2026-01-01");

        result.AgentActivity.Count.ShouldBe(3);
        result.AgentActivity[0].AgentName.ShouldBe("alpha-agent");
        result.AgentActivity[1].AgentName.ShouldBe("beta-agent");
        result.AgentActivity[2].AgentName.ShouldBe("zebra-agent");
    }

    [Fact]
    public void GetDigest_WhenDirectoryNameIsNotValidAgentSlug_IgnoresDirectory()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentsDir = paths.AgentsDir(projectSlug);
        Directory.CreateDirectory(agentsDir);

        // Create valid and invalid agent directories
        Directory.CreateDirectory(Path.Combine(agentsDir, "valid-agent"));
        Directory.CreateDirectory(Path.Combine(agentsDir, "-invalid")); // Invalid: starts with dash
        Directory.CreateDirectory(Path.Combine(agentsDir, "a")); // Invalid: too short

        var result = service.GetDigest(projectSlug, "2026-01-01");

        result.AgentActivity.Count.ShouldBe(1);
        result.AgentActivity[0].AgentSlug.ShouldBe("valid-agent");
    }

    private static DigestService CreateService(out WorkspacePaths paths)
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        paths = new WorkspacePaths(root);
        var modelRegistry = Substitute.For<IModelRegistry>();
        modelRegistry.Find(Arg.Any<string>(), Arg.Any<string>()).Returns((ModelDescriptor?)null);
        var agentService = new AgentService(
            paths,
            new AgentTemplatesService(paths),
            new AtomicFileWriter(),
            new ProjectMutationCoordinator(),
            modelRegistry,
            NullLogger<AgentService>.Instance);
        return new DigestService(paths, agentService);
    }
}
