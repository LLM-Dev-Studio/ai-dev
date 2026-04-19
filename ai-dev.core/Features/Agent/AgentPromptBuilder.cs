using AiDev.Features.KnowledgeBase;
using AiDev.Features.Playbook;

namespace AiDev.Features.Agent;

/// <summary>
/// Assembles the effective prompt for an agent session by injecting KB context
/// and playbook instructions ahead of the base prompt.
/// </summary>
public class AgentPromptBuilder(
    KbService kbService,
    PlaybookService playbookService,
    ILogger<AgentPromptBuilder> logger)
{
    public string Build(ProjectSlug projectSlug, AgentSlug agentSlug,
        string projectScopedPrefix, string basePrompt,
        string inboxDir, string[] inboxSnapshot)
    {
        var effectivePrompt = string.Join("\n\n", projectScopedPrefix, basePrompt);
        var key = $"{projectSlug.Value}/{agentSlug.Value}";

        var inboxText = ReadInboxText(inboxDir, inboxSnapshot);

        var kbContext = kbService.BuildInjectionContext(projectSlug, inboxText);
        if (!string.IsNullOrEmpty(kbContext))
        {
            effectivePrompt = kbContext + "\n\n---\n\n" + effectivePrompt;
            logger.LogInformation("[runner] Injected KB context into prompt for {Key}", key);
        }

        var playbookSlug = ExtractPlaybookSlug(inboxDir, inboxSnapshot);
        if (playbookSlug != null)
        {
            var playbookContext = playbookService.GetInjectionContext(projectSlug, playbookSlug);
            if (!string.IsNullOrEmpty(playbookContext))
            {
                effectivePrompt = playbookContext + "\n\n---\n\n" + effectivePrompt;
                logger.LogInformation("[runner] Injected playbook '{Slug}' into prompt for {Key}", playbookSlug, key);
            }
            else
            {
                logger.LogWarning("[runner] Playbook '{Slug}' specified in inbox message not found for {Key}", playbookSlug, key);
            }
        }

        return effectivePrompt;
    }

    private static string? ExtractPlaybookSlug(string inboxDir, string[] snapshot)
    {
        foreach (var filename in snapshot)
        {
            try
            {
                var path = Path.Combine(inboxDir, filename);
                if (!File.Exists(path)) continue;
                var content = File.ReadAllText(path);
                var (fields, _) = FrontmatterParser.Parse(content);
                if (fields.TryGetValue("playbook", out var slug) && !string.IsNullOrWhiteSpace(slug))
                    return slug.Trim();
            }
            catch { /* ignore unreadable files */ }
        }
        return null;
    }

    private static string ReadInboxText(string inboxDir, string[] snapshot)
    {
        if (snapshot.Length == 0) return string.Empty;
        var sb = new System.Text.StringBuilder();
        foreach (var filename in snapshot)
        {
            try
            {
                var path = Path.Combine(inboxDir, filename);
                if (File.Exists(path)) sb.AppendLine(File.ReadAllText(path));
            }
            catch { /* ignore unreadable files */ }
        }
        return sb.ToString();
    }
}
