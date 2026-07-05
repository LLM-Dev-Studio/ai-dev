namespace AiDev.Features.KnowledgeBase;

/// <summary>
/// Provides CRUD and prompt-injection operations for project knowledge base articles.
/// </summary>
public class KbService(WorkspacePaths paths, AtomicFileWriter fileWriter, ProjectMutationCoordinator coordinator)
{
    /// <summary>
    /// Lists the knowledge base articles for the specified project.
    /// </summary>
    /// <param name="projectSlug">The project slug to inspect.</param>
    /// <returns>The project knowledge base articles ordered by title.</returns>
    public List<KbArticle> ListArticles(ProjectSlug projectSlug)
    {
        var dir = paths.KbDir(projectSlug);
        if (!Directory.Exists(dir)) return [];

        return [.. Directory.GetFiles(dir, "*.md")
            .Select(f =>
            {
                var content = File.ReadAllText(f);
                var slug = Path.GetFileNameWithoutExtension(f);
                var title = ExtractTitle(content) ?? slug;
                var (fields, _) = FrontmatterParser.Parse(content);
                var trigger = fields.TryGetValue("trigger", out var t) && !string.IsNullOrWhiteSpace(t) ? t : null;
                return new KbArticle { Slug = slug, Title = title, Trigger = trigger };
            })
            .OrderBy(a => a.Title)];
    }

    /// <summary>
    /// Reads all KB articles for the project and returns a formatted context block for
    /// injection into the agent prompt. Articles without a <c>trigger:</c> frontmatter field
    /// are always included. Articles with a trigger are included only when at least one trigger
    /// word appears (case-insensitive) in <paramref name="messageText"/>.
    /// Returns <c>null</c> when no articles qualify (or the KB directory does not exist).
    /// </summary>
    public string? BuildInjectionContext(ProjectSlug projectSlug, string messageText)
    {
        var dir = paths.KbDir(projectSlug);
        if (!Directory.Exists(dir)) return null;

        var files = Directory.GetFiles(dir, "*.md");
        if (files.Length == 0) return null;

        var sb = new System.Text.StringBuilder();

        foreach (var filePath in files.OrderBy(f => f))
        {
            var content = File.ReadAllText(filePath);
            var (fields, body) = FrontmatterParser.Parse(content);

            var hasTrigger = fields.TryGetValue("trigger", out var trigger) && !string.IsNullOrWhiteSpace(trigger);
            if (hasTrigger)
            {
                var words = trigger!.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var matched = words.Any(w => messageText.Contains(w, StringComparison.OrdinalIgnoreCase));
                if (!matched) continue;
            }

            var slug = Path.GetFileNameWithoutExtension(filePath);
            var title = ExtractTitle(content) ?? slug;
            var articleBody = string.IsNullOrWhiteSpace(body) ? content : body;

            if (sb.Length > 0) sb.AppendLine();
            sb.AppendLine($"### {title}");
            sb.AppendLine();
            sb.Append(articleBody.TrimEnd());
            sb.AppendLine();
        }

        if (sb.Length == 0) return null;

        return "## Knowledge Base\n\n" + sb.ToString();
    }

    /// <summary>
    /// Gets the raw content of a knowledge base article.
    /// </summary>
    /// <param name="projectSlug">The project slug that owns the article.</param>
    /// <param name="slug">The article slug.</param>
    /// <returns>The article content, or an empty string when the article does not exist.</returns>
    public string GetContent(ProjectSlug projectSlug, string slug)
    {
        var path = paths.SafeKbArticlePath(projectSlug, slug);
        return path != null && File.Exists(path.Value) ? File.ReadAllText(path.Value) : string.Empty;
    }

    /// <summary>
    /// Saves content to a knowledge base article.
    /// </summary>
    /// <param name="projectSlug">The project slug that owns the article.</param>
    /// <param name="slug">The article slug.</param>
    /// <param name="content">The article content to save.</param>
    /// <returns>The result of the save operation.</returns>
    public Result<Unit> Save(ProjectSlug projectSlug, string slug, string content)
    {
        var path = paths.SafeKbArticlePath(projectSlug, slug);
        if (path == null) return new Err<Unit>(new DomainError("KB_INVALID_SLUG", "Invalid article slug."));
        return coordinator.Execute<Result<Unit>>(projectSlug, () =>
        {
            try
            {
                fileWriter.WriteAllText(path.Value, content);
                return new Ok<Unit>(Unit.Value);
            }
            catch (IOException ex) { return new Err<Unit>(new DomainError("KB_IO_ERROR", ex.Message)); }
            catch (UnauthorizedAccessException ex) { return new Err<Unit>(new DomainError("KB_IO_ERROR", ex.Message)); }
        });
    }

    /// <summary>
    /// Creates a new knowledge base article with default content.
    /// </summary>
    /// <param name="projectSlug">The project slug that will own the article.</param>
    /// <param name="slug">The article slug.</param>
    /// <returns>The result of the create operation.</returns>
    public Result<Unit> Create(ProjectSlug projectSlug, string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return new Err<Unit>(new DomainError("KB_SLUG_REQUIRED", "Slug is required."));
        if (slug.Contains("..") || slug.Contains('/') || slug.Contains('\\')) return new Err<Unit>(new DomainError("KB_INVALID_SLUG", "Invalid slug."));

        var path = Path.Combine(paths.KbDir(projectSlug), $"{slug}.md");
        if (File.Exists(path)) return new Err<Unit>(new DomainError("KB_ALREADY_EXISTS", $"Article '{slug}' already exists."));

        return Save(projectSlug, slug, $"# {slug}\n\n");
    }

    /// <summary>
    /// Deletes a knowledge base article when it exists.
    /// </summary>
    /// <param name="projectSlug">The project slug that owns the article.</param>
    /// <param name="slug">The article slug.</param>
    public void Delete(ProjectSlug projectSlug, string slug)
    {
        var path = paths.SafeKbArticlePath(projectSlug, slug);
        if (path == null) return;

        coordinator.Execute(projectSlug, () =>
        {
            fileWriter.DeleteFile(path.Value);
            return Unit.Value;
        });
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
