namespace AiDevNet.Features.Journal;

public class JournalsService(WorkspacePaths paths)
{
    public List<JournalEntry> ListDates(ProjectSlug projectSlug, Models.AgentSlug agentSlug)
    {
        var dir = paths.AgentJournalDir(projectSlug, agentSlug);
        if (!Directory.Exists(dir)) return [];

        return Directory.GetFiles(dir, "*.md")
            .Select(f =>
            {
                var name = Path.GetFileNameWithoutExtension(f);
                return new JournalEntry { Date = name, Filename = Path.GetFileName(f) };
            })
            .OrderByDescending(e => e.Date)
            .ToList();
    }

    public string GetEntry(ProjectSlug projectSlug, Models.AgentSlug agentSlug, string date)
    {
        var path = Path.Combine(paths.AgentJournalDir(projectSlug, agentSlug), $"{date}.md");
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
