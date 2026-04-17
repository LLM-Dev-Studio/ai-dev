using AiDev.Services;

namespace AiDevNet.Tests.Unit;

public class MessagesServiceTests
{
    [Fact]
    public void ListMessages_WhenAgentsDirDoesNotExist_ReturnsEmptyList()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("test-project");

        var result = service.ListMessages(projectSlug);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void ListMessages_WhenNoAgentsHaveMessages_ReturnsEmptyList()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentsDir = paths.AgentsDir(projectSlug);
        Directory.CreateDirectory(Path.Combine(agentsDir, "agent1"));

        var result = service.ListMessages(projectSlug);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void ListMessages_WhenInboxHasMessages_IncludesThemAsUnprocessed()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentSlug = new AgentSlug("agent1");
        var inboxDir = paths.AgentInboxDir(projectSlug, agentSlug);
        Directory.CreateDirectory(inboxDir.Value);

        var messageContent = @"---
date: 2026-01-15T10:30:00Z
from: user@example.com
to: agent1@example.com
re: Test Message
type: email
priority: high
---
This is the message body.";
        File.WriteAllText(Path.Combine(inboxDir.Value, "msg-001.md"), messageContent);

        var result = service.ListMessages(projectSlug);

        result.Count.ShouldBe(1);
        result[0].IsProcessed.ShouldBeFalse();
        result[0].From.ShouldBe("user@example.com");
        result[0].Priority.Value.ShouldBe("high");
    }

    [Fact]
    public void ListMessages_WhenProcessedDirHasMessages_IncludesThemAsProcessed()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentSlug = new AgentSlug("agent1");
        var processedDir = paths.AgentInboxProcessedDir(projectSlug, agentSlug);
        Directory.CreateDirectory(processedDir.Value);

        var messageContent = @"---
date: 2026-01-15T10:30:00Z
from: user@example.com
to: agent1@example.com
re: Processed Message
type: email
---
Processed message body.";
        File.WriteAllText(Path.Combine(processedDir.Value, "msg-002.md"), messageContent);

        var result = service.ListMessages(projectSlug);

        result.Count.ShouldBe(1);
        result[0].IsProcessed.ShouldBeTrue();
    }

    [Fact]
    public void ListMessages_WithSpecificAgent_ReturnsOnlyThatAgentMessages()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agent1 = new AgentSlug("agent1");
        var agent2 = new AgentSlug("agent2");

        var createMessage = (ProjectSlug proj, AgentSlug agent, string name) =>
        {
            var dir = paths.AgentInboxDir(proj, agent);
            Directory.CreateDirectory(dir.Value);
            var content = @$"---
date: 2026-01-15T10:30:00Z
from: user@{agent.Value}@example.com
to: {agent.Value}@example.com
re: Message for {agent.Value}
type: email
---
Body";
            File.WriteAllText(Path.Combine(dir.Value, name), content);
        };

        createMessage(projectSlug, agent1, "msg1.md");
        createMessage(projectSlug, agent2, "msg2.md");

        var result = service.ListMessages(projectSlug, agent1);

        result.Count.ShouldBe(1);
        result[0].From.ShouldContain("agent1");
    }

    [Fact]
    public void ListMessages_ReturnsSortedByDateDescending()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentSlug = new AgentSlug("agent1");
        var inboxDir = paths.AgentInboxDir(projectSlug, agentSlug);
        Directory.CreateDirectory(inboxDir.Value);

        var createMsg = (string date, string file) =>
        {
            var content = $@"---
date: {date}
from: user@example.com
to: agent@example.com
re: Message
type: email
---
Body";
            File.WriteAllText(Path.Combine(inboxDir.Value, file), content);
        };

        createMsg("2026-01-10T10:00:00Z", "msg1.md");
        createMsg("2026-01-15T10:00:00Z", "msg2.md");
        createMsg("2026-01-12T10:00:00Z", "msg3.md");

        var result = service.ListMessages(projectSlug);

        result.Count.ShouldBe(3);
        result[0].Date.Value.Day.ShouldBe(15);
        result[1].Date.Value.Day.ShouldBe(12);
        result[2].Date.Value.Day.ShouldBe(10);
    }

    [Fact]
    public void ListMessages_WithMissingOptionalFields_UsesDefaults()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentSlug = new AgentSlug("agent1");
        var inboxDir = paths.AgentInboxDir(projectSlug, agentSlug);
        Directory.CreateDirectory(inboxDir.Value);

        var messageContent = @"---
from: user@example.com
to: agent@example.com
re: Minimal Message
type: email
---
Body without date or priority";
        File.WriteAllText(Path.Combine(inboxDir.Value, "minimal.md"), messageContent);

        var result = service.ListMessages(projectSlug);

        result.Count.ShouldBe(1);
        result[0].Date.ShouldBeNull();
        result[0].Priority.Value.ShouldBe("normal");
        result[0].TaskId.ShouldBeNull();
        result[0].Playbook.ShouldBeNull();
    }

    [Fact]
    public void ListMessages_IgnoresFilesWithoutMdExtension()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentSlug = new AgentSlug("agent1");
        var inboxDir = paths.AgentInboxDir(projectSlug, agentSlug);
        Directory.CreateDirectory(inboxDir.Value);

        var validContent = @"---
from: user@example.com
to: agent@example.com
re: Valid
type: email
---
Body";
        File.WriteAllText(Path.Combine(inboxDir.Value, "valid.md"), validContent);
        File.WriteAllText(Path.Combine(inboxDir.Value, "invalid.txt"), "text file");
        File.WriteAllText(Path.Combine(inboxDir.Value, "invalid.json"), "{}");

        var result = service.ListMessages(projectSlug);

        result.Count.ShouldBe(1);
        result[0].Filename.ShouldBe("valid.md");
    }

    [Fact]
    public void ListMessages_IgnoresInvalidAgentDirectories()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentsDir = paths.AgentsDir(projectSlug);
        Directory.CreateDirectory(agentsDir);

        // Create valid and invalid agent directories
        var validAgent = new AgentSlug("valid-agent");
        var validInboxDir = paths.AgentInboxDir(projectSlug, validAgent);
        Directory.CreateDirectory(validInboxDir.Value);
        var validMsg = @"---
from: user@example.com
to: agent@example.com
re: Valid
type: email
---
Body";
        File.WriteAllText(Path.Combine(validInboxDir.Value, "msg.md"), validMsg);

        // Invalid agent directory (starts with dash)
        Directory.CreateDirectory(Path.Combine(agentsDir, "-invalid"));

        var result = service.ListMessages(projectSlug);

        result.Count.ShouldBe(1);
        result[0].AgentSlug.Value.ShouldBe("valid-agent");
    }

    [Fact]
    public void MarkProcessed_WhenMessageExists_MovesFile()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentSlug = new AgentSlug("agent1");
        var inboxDir = paths.AgentInboxDir(projectSlug, agentSlug);
        var processedDir = paths.AgentInboxProcessedDir(projectSlug, agentSlug);
        Directory.CreateDirectory(inboxDir.Value);

        var messageContent = @"---
from: user@example.com
to: agent@example.com
re: Message
type: email
---
Body";
        var filename = "msg.md";
        File.WriteAllText(Path.Combine(inboxDir.Value, filename), messageContent);

        service.MarkProcessed(projectSlug, agentSlug, filename);

        File.Exists(Path.Combine(inboxDir.Value, filename)).ShouldBeFalse();
        File.Exists(Path.Combine(processedDir.Value, filename)).ShouldBeTrue();
    }

    [Fact]
    public void MarkProcessed_WhenMessageDoesNotExist_IsNoOp()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentSlug = new AgentSlug("agent1");

        // Should not throw
        service.MarkProcessed(projectSlug, agentSlug, "nonexistent.md");
    }

    [Fact]
    public void MarkProcessed_CreatesProcessedDirectoryIfMissing()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentSlug = new AgentSlug("agent1");
        var inboxDir = paths.AgentInboxDir(projectSlug, agentSlug);
        var processedDir = paths.AgentInboxProcessedDir(projectSlug, agentSlug);
        Directory.CreateDirectory(inboxDir.Value);

        var filename = "msg.md";
        File.WriteAllText(Path.Combine(inboxDir.Value, filename), "content");

        service.MarkProcessed(projectSlug, agentSlug, filename);

        Directory.Exists(processedDir.Value).ShouldBeTrue();
        File.Exists(Path.Combine(processedDir.Value, filename)).ShouldBeTrue();
    }

    [Fact]
    public void MarkProcessed_WhenFileAlreadyExistsInProcessed_Overwrites()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentSlug = new AgentSlug("agent1");
        var inboxDir = paths.AgentInboxDir(projectSlug, agentSlug);
        var processedDir = paths.AgentInboxProcessedDir(projectSlug, agentSlug);
        Directory.CreateDirectory(inboxDir.Value);
        Directory.CreateDirectory(processedDir.Value);

        var filename = "msg.md";
        File.WriteAllText(Path.Combine(inboxDir.Value, filename), "new content");
        File.WriteAllText(Path.Combine(processedDir.Value, filename), "old content");

        service.MarkProcessed(projectSlug, agentSlug, filename);

        var content = File.ReadAllText(Path.Combine(processedDir.Value, filename));
        content.ShouldBe("new content");
    }

    [Fact]
    public void ListMessages_WithTaskIdInFrontmatter_ParsesTaskId()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentSlug = new AgentSlug("agent1");
        var inboxDir = paths.AgentInboxDir(projectSlug, agentSlug);
        Directory.CreateDirectory(inboxDir.Value);

        var messageContent = @"---
from: user@example.com
to: agent@example.com
re: Message with Task
type: email
task-id: task-1234567890123-abcde
---
Body";
        File.WriteAllText(Path.Combine(inboxDir.Value, "msg.md"), messageContent);

        var result = service.ListMessages(projectSlug);

        result[0].TaskId.ShouldNotBeNull();
        result[0].TaskId.Value.ToString().ShouldBe("task-1234567890123-abcde");
    }

    [Fact]
    public void ListMessages_WithPlaybookField_ParsesPlaybook()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentSlug = new AgentSlug("agent1");
        var inboxDir = paths.AgentInboxDir(projectSlug, agentSlug);
        Directory.CreateDirectory(inboxDir.Value);

        var messageContent = @"---
from: user@example.com
to: agent@example.com
re: Message with Playbook
type: email
playbook: deploy-process
---
Body";
        File.WriteAllText(Path.Combine(inboxDir.Value, "msg.md"), messageContent);

        var result = service.ListMessages(projectSlug);

        result[0].Playbook.ShouldBe("deploy-process");
    }

    private static MessagesService CreateService(out WorkspacePaths paths)
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        paths = new WorkspacePaths(root);
        return new MessagesService(paths);
    }
}
