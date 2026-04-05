using AiDev.Services;

namespace AiDev.Features.Playbook;

public class PlaybookService(WorkspacePaths paths)
{
    public List<PlaybookItem> ListPlaybooks(ProjectSlug projectSlug)
    {
        var dir = paths.PlaybooksDir(projectSlug);
        if (!Directory.Exists(dir)) return [];

        return Directory.GetFiles(dir, "*.md")
            .Select(f =>
            {
                var content = File.ReadAllText(f);
                var slug = Path.GetFileNameWithoutExtension(f);
                var title = ExtractTitle(content) ?? slug;
                var (fields, _) = FrontmatterParser.Parse(content);
                var macro = fields.TryGetValue("macro", out var m) && !string.IsNullOrWhiteSpace(m) ? m : null;
                return new PlaybookItem { Slug = slug, Title = title, Macro = macro };
            })
            .OrderBy(p => p.Title)
            .ToList();
    }

    /// <summary>
    /// Loads a playbook by slug and returns its body content formatted for prompt injection,
    /// or <c>null</c> if the playbook does not exist or the slug is invalid.
    /// </summary>
    public string? GetInjectionContext(ProjectSlug projectSlug, string slug)
    {
        var path = paths.SafePlaybookPath(projectSlug, slug);
        if (path == null || !File.Exists(path.Value)) return null;

        var content = File.ReadAllText(path.Value);
        var (_, body) = FrontmatterParser.Parse(content);
        var playbookBody = string.IsNullOrWhiteSpace(body) ? content : body;

        var title = ExtractTitle(content) ?? slug;
        return $"## Playbook: {title}\n\n{playbookBody.TrimEnd()}";
    }

    public string GetContent(ProjectSlug projectSlug, string slug)
    {
        var path = paths.SafePlaybookPath(projectSlug, slug);
        return path != null && File.Exists(path.Value) ? File.ReadAllText(path.Value) : string.Empty;
    }

    public string? Save(ProjectSlug projectSlug, string slug, string content)
    {
        var path = paths.SafePlaybookPath(projectSlug, slug);
        if (path == null) return "Invalid playbook slug.";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path.Value)!);
            File.WriteAllText(path.Value, content);
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }

    public string? Create(ProjectSlug projectSlug, string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return "Slug is required.";
        if (slug.Contains("..") || slug.Contains('/') || slug.Contains('\\')) return "Invalid slug.";

        var dir = paths.PlaybooksDir(projectSlug);
        var path = Path.Combine(dir, $"{slug}.md");
        if (File.Exists(path)) return $"Playbook '{slug}' already exists.";

        return Save(projectSlug, slug, $"# {slug}\n\n");
    }

    public void Delete(ProjectSlug projectSlug, string slug)
    {
        var path = paths.SafePlaybookPath(projectSlug, slug);
        if (path != null && File.Exists(path.Value)) File.Delete(path.Value);
    }

    private static string? ExtractTitle(string content)
    {
        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("# "))
                return trimmed[2..].Trim();
        }
        return null;
    }
}
