using AiDev.Features.Journal;

namespace AiDevNet.Tests.Unit;

public class JournalsServiceTests
{
    [Fact]
    public void ListDates_WhenDirectoryDoesNotExist_ReturnsEmptyList()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("test-project");
        var agentSlug = new AgentSlug("test-agent");

        var result = service.ListDates(projectSlug, agentSlug);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void ListDates_WhenDirectoryExists_ReturnsEntriesOrderedByDateDescending()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentSlug = new AgentSlug("test-agent");
        var dir = paths.AgentJournalDir(projectSlug, agentSlug);
        Directory.CreateDirectory(dir);

        // Create journal entries with different dates
        File.WriteAllText(Path.Combine(dir, "2026-01-01.md"), "Entry 1");
        File.WriteAllText(Path.Combine(dir, "2026-01-03.md"), "Entry 3");
        File.WriteAllText(Path.Combine(dir, "2026-01-02.md"), "Entry 2");

        var result = service.ListDates(projectSlug, agentSlug);

        result.Count.ShouldBe(3);
        result[0].Date.ShouldBe("2026-01-03");
        result[1].Date.ShouldBe("2026-01-02");
        result[2].Date.ShouldBe("2026-01-01");
    }

    [Fact]
    public void ListDates_WhenDirectoryExists_IncludesFilenameInResult()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentSlug = new AgentSlug("test-agent");
        var dir = paths.AgentJournalDir(projectSlug, agentSlug);
        Directory.CreateDirectory(dir);

        File.WriteAllText(Path.Combine(dir, "2026-01-01.md"), "Entry");

        var result = service.ListDates(projectSlug, agentSlug);

        result.Count.ShouldBe(1);
        result[0].Filename.ShouldBe("2026-01-01.md");
    }

    [Fact]
    public void ListDates_WhenDirectoryHasNonMarkdownFiles_IgnoresThem()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentSlug = new AgentSlug("test-agent");
        var dir = paths.AgentJournalDir(projectSlug, agentSlug);
        Directory.CreateDirectory(dir);

        File.WriteAllText(Path.Combine(dir, "2026-01-01.md"), "Entry");
        File.WriteAllText(Path.Combine(dir, "2026-01-02.txt"), "Not markdown");
        File.WriteAllText(Path.Combine(dir, "2026-01-03.json"), "JSON file");

        var result = service.ListDates(projectSlug, agentSlug);

        result.Count.ShouldBe(1);
        result[0].Filename.ShouldBe("2026-01-01.md");
    }

    [Fact]
    public void GetEntry_WhenFileDoesNotExist_ReturnsEmptyString()
    {
        var service = CreateService(out _);
        var projectSlug = new ProjectSlug("test-project");
        var agentSlug = new AgentSlug("test-agent");

        var result = service.GetEntry(projectSlug, agentSlug, "2026-01-01");

        result.ShouldBeEmpty();
    }

    [Fact]
    public void GetEntry_WhenFileExists_ReturnsFileContent()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("test-project");
        var agentSlug = new AgentSlug("test-agent");
        var dir = paths.AgentJournalDir(projectSlug, agentSlug);
        Directory.CreateDirectory(dir);
        var content = "# Journal Entry\n\nThis is my entry for today.";
        File.WriteAllText(Path.Combine(dir, "2026-01-01.md"), content);

        var result = service.GetEntry(projectSlug, agentSlug, "2026-01-01");

        result.ShouldBe(content);
    }

    [Fact]
    public void GetEntry_WhenFilePathContainsSpecialCharacters_HandlesCorrectly()
    {
        var service = CreateService(out var paths);
        var projectSlug = new ProjectSlug("my-project");
        var agentSlug = new AgentSlug("my-agent");
        var dir = paths.AgentJournalDir(projectSlug, agentSlug);
        Directory.CreateDirectory(dir);
        var content = "Content with special chars: éàü";
        File.WriteAllText(Path.Combine(dir, "2026-01-01.md"), content, System.Text.Encoding.UTF8);

        var result = service.GetEntry(projectSlug, agentSlug, "2026-01-01");

        result.ShouldBe(content);
    }

    private static JournalsService CreateService(out WorkspacePaths paths)
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        paths = new WorkspacePaths(root);
        return new JournalsService(paths);
    }
}
