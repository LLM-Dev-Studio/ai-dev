namespace AiDevNet.Services;

public class JournalEntry
{
    public string Date { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
}

public class JournalsService(WorkspaceService workspace)
{
    private string JournalDir(string projectSlug, string agentSlug) =>
        Path.Combine(workspace.GetProjectPath(projectSlug), "agents", agentSlug, "journal");

    public List<JournalEntry> ListDates(string projectSlug, string agentSlug)
    {
        var dir = JournalDir(projectSlug, agentSlug);
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

    public string GetEntry(string projectSlug, string agentSlug, string date)
    {
        var path = Path.Combine(JournalDir(projectSlug, agentSlug), $"{date}.md");
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
