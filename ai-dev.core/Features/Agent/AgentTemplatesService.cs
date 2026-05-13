namespace AiDev.Features.Agent;

/// <summary>
/// Loads, saves, creates, and deletes agent templates.
/// </summary>
public class AgentTemplatesService(WorkspacePaths paths)
{
    private readonly TemplateComposer _composer = new();

    /// <summary>
    /// Lists the available agent templates.
    /// </summary>
    /// <returns>The available templates ordered by name.</returns>
    public List<AgentTemplate> ListTemplates()
    {
        var dir = paths.AgentTemplatesDir;
        if (!Directory.Exists(dir)) return [];

        return [.. Directory.GetFiles(dir, "*.json")
            .Select(LoadTemplateFile)
            .OfType<AgentTemplate>()
            .OrderBy(t => t.Name)];
    }

    private IReadOnlyDictionary<string, string> LoadPartials()
    {
        var sharedDir = Path.Combine(paths.AgentTemplatesDir.Value, "shared");
        if (!Directory.Exists(sharedDir)) return new Dictionary<string, string>();

        return Directory.GetFiles(sharedDir, "*.md")
            .ToDictionary(
                f => $"shared/{Path.GetFileNameWithoutExtension(f)}",
                f => File.ReadAllText(f));
    }

    private AgentTemplate? LoadTemplateFile(string jsonPath)
    {
        try
        {
            var json = File.ReadAllText(jsonPath);
            var t = JsonSerializer.Deserialize<AgentTemplate>(json, JsonDefaults.Write);
            if (t is null) return null;

            var partials = LoadPartials();

            var mdPath = Path.ChangeExtension(jsonPath, ".md");
            if (File.Exists(mdPath))
                t.Content = _composer.Compose(File.ReadAllText(mdPath), partials);

            var compactPath = Path.Combine(
                Path.GetDirectoryName(jsonPath)!,
                $"{Path.GetFileNameWithoutExtension(jsonPath)}.compact.md");
            if (File.Exists(compactPath))
                t.CompactContent = _composer.Compose(File.ReadAllText(compactPath), partials);

            return t;
        }
        catch { return null; }
    }

    /// <summary>
    /// Gets an agent template by slug.
    /// </summary>
    /// <param name="slug">The template slug.</param>
    /// <returns>The matching template, or <see langword="null"/> when not found.</returns>
    public AgentTemplate? GetTemplate(string slug)
    {
        if (!AgentSlug.TryParse(slug, out _)) return null;
        var jsonPath = paths.SafeTemplatePath(slug, ".json");
        return jsonPath != null && File.Exists(jsonPath.Value) ? LoadTemplateFile(jsonPath.Value) : null;
    }

    /// <summary>
    /// Saves an agent template.
    /// </summary>
    /// <param name="template">The template to persist.</param>
    public void SaveTemplate(AgentTemplate template)
    {
        var jsonPath = paths.SafeTemplatePath(template.Slug, ".json");
        var mdPath = paths.SafeTemplatePath(template.Slug, ".md");
        if (jsonPath == null || mdPath == null)
            throw new ArgumentException($"Invalid template slug: '{template.Slug}'");

        Directory.CreateDirectory(paths.AgentTemplatesDir.Value);

        if (!string.IsNullOrEmpty(template.Content))
            File.WriteAllText(mdPath.Value, template.Content);

        // Store metadata only in JSON (content lives in .md)
        var meta = new AgentTemplate
        {
            Slug = template.Slug,
            Name = template.Name,
            Role = template.Role,
            Model = template.Model,
            Executor = string.IsNullOrWhiteSpace(template.Executor) ? null : template.Executor,
            Skills = template.Skills is { Count: > 0 } ? template.Skills : [],
            Description = template.Description,
            Content = "",
            ThinkingLevel = template.ThinkingLevel,
        };
        File.WriteAllText(jsonPath.Value, JsonSerializer.Serialize(meta, JsonDefaults.Write));
    }

    /// <summary>
    /// Creates and saves a new agent template.
    /// </summary>
    /// <param name="template">The template to create.</param>
    /// <returns>The created template.</returns>
    public AgentTemplate CreateTemplate(AgentTemplate template)
    {
        if (string.IsNullOrEmpty(template.Model))
            template.Model = "claude-sonnet-4-6";

        SaveTemplate(template);
        return template;
    }

    /// <summary>
    /// Deletes an agent template by slug.
    /// </summary>
    /// <param name="slug">The template slug.</param>
    public void DeleteTemplate(string slug)
    {
        var jsonPath = paths.SafeTemplatePath(slug, ".json");
        var mdPath = paths.SafeTemplatePath(slug, ".md");

        if (jsonPath != null && File.Exists(jsonPath.Value)) File.Delete(jsonPath.Value);
        if (mdPath != null && File.Exists(mdPath.Value)) File.Delete(mdPath.Value);
    }
}
