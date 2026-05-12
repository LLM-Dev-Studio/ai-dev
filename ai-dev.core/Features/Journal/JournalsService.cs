namespace AiDev.Features.Journal;

/// <summary>
/// Provides access to agent journal entries.
/// </summary>
public class JournalsService(WorkspacePaths paths)
{
    /// <summary>
    /// Lists journal entries for the specified agent.
    /// </summary>
    /// <param name="projectSlug">The project slug that owns the agent.</param>
    /// <param name="agentSlug">The agent slug whose journal is being read.</param>
    /// <returns>The journal entries ordered by descending date.</returns>
    public List<JournalEntry> ListDates(ProjectSlug projectSlug, AgentSlug agentSlug)
    {
        var dir = paths.AgentJournalDir(projectSlug, agentSlug);
        if (!Directory.Exists(dir)) return [];

        return [.. Directory.GetFiles(dir, "*.md")
            .Select(f =>
            {
                var name = Path.GetFileNameWithoutExtension(f);
                return new JournalEntry { Date = name, Filename = Path.GetFileName(f) };
            })
            .OrderByDescending(e => e.Date)];
    }

    /// <summary>
    /// Gets the content of a journal entry for the specified date.
    /// </summary>
    /// <param name="projectSlug">The project slug that owns the agent.</param>
    /// <param name="agentSlug">The agent slug whose journal is being read.</param>
    /// <param name="date">The journal entry date key.</param>
    /// <returns>The journal entry content, or an empty string when the entry does not exist.</returns>
    public string GetEntry(ProjectSlug projectSlug, AgentSlug agentSlug, string date)
    {
        var path = Path.Combine(paths.AgentJournalDir(projectSlug, agentSlug), $"{date}.md");
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
