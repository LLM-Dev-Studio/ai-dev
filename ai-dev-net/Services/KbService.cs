namespace AiDevNet.Services;

public class KbArticle
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public class KbService(WorkspacePaths paths)
{
    public List<KbArticle> ListArticles(ProjectSlug projectSlug)
    {
        var dir = paths.KbDir(projectSlug);
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

    public string GetContent(ProjectSlug projectSlug, string slug)
    {
        var path = paths.SafeKbArticlePath(projectSlug, slug);
        return path != null && File.Exists(path.Value) ? File.ReadAllText(path.Value) : string.Empty;
    }

    public string? Save(ProjectSlug projectSlug, string slug, string content)
    {
        var path = paths.SafeKbArticlePath(projectSlug, slug);
        if (path == null) return "Invalid article slug.";
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

        var path = Path.Combine(paths.KbDir(projectSlug), $"{slug}.md");
        if (File.Exists(path)) return $"Article '{slug}' already exists.";

        return Save(projectSlug, slug, $"# {slug}\n\n");
    }

    public void Delete(ProjectSlug projectSlug, string slug)
    {
        var path = paths.SafeKbArticlePath(projectSlug, slug);
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
