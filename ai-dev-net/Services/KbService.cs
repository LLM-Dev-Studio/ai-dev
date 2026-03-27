namespace AiDevNet.Services;

public class KbArticle
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public class KbService(WorkspaceService workspace)
{
    private string KbDir(string projectSlug) =>
        Path.Combine(workspace.GetProjectPath(projectSlug), "kb");

    /// <summary>
    /// Returns the canonical .md path for a KB article slug, or null if it would escape the kb directory.
    /// </summary>
    private string? SafeArticlePath(string projectSlug, string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var dir = Path.GetFullPath(KbDir(projectSlug));
        var resolved = Path.GetFullPath(Path.Combine(dir, $"{slug}.md"));
        return resolved.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? resolved : null;
    }

    public List<KbArticle> ListArticles(string projectSlug)
    {
        var dir = KbDir(projectSlug);
        if (!Directory.Exists(dir)) return [];

        return Directory.GetFiles(dir, "*.md")
            .Select(f =>
            {
                var slug = Path.GetFileNameWithoutExtension(f);
                var title = ExtractTitle(File.ReadAllText(f)) ?? slug;
                return new KbArticle { Slug = slug, Title = title };
            })
            .OrderBy(a => a.Title)
            .ToList();
    }

    public string GetContent(string projectSlug, string slug)
    {
        var path = SafeArticlePath(projectSlug, slug);
        return path != null && File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    public string? Save(string projectSlug, string slug, string content)
    {
        var path = SafeArticlePath(projectSlug, slug);
        if (path == null) return "Invalid article slug.";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }

    public string? Create(string projectSlug, string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return "Slug is required.";
        if (slug.Contains("..") || slug.Contains('/') || slug.Contains('\\')) return "Invalid slug.";

        var path = Path.Combine(KbDir(projectSlug), $"{slug}.md");
        if (File.Exists(path)) return $"Article '{slug}' already exists.";

        return Save(projectSlug, slug, $"# {slug}\n\n");
    }

    public void Delete(string projectSlug, string slug)
    {
        var path = SafeArticlePath(projectSlug, slug);
        if (path != null && File.Exists(path)) File.Delete(path);
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
