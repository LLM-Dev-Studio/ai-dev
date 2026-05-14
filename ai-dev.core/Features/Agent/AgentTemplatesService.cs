namespace AiDev.Features.Agent;

/// <summary>
/// Loads, saves, creates, and deletes agent templates from the global templates directory.
/// </summary>
public class AgentTemplatesService
{
    private readonly TemplateComposer _composer = new();
    private readonly string? _templatesOverride;

    public AgentTemplatesService(string templatesDir) => _templatesOverride = templatesDir;
    public AgentTemplatesService() { }

    private AgentTemplatesFile TemplatesDir => new(_templatesOverride ?? GlobalPaths.AgentTemplatesDir);

    /// <summary>
    /// Lists the available agent templates.
    /// </summary>
    public List<AgentTemplate> ListTemplates()
    {
        var dir = TemplatesDir.Value;
        if (!Directory.Exists(dir)) return [];

        return [.. Directory.GetFiles(dir, "*.json")
            .Select(LoadTemplateFile)
            .OfType<AgentTemplate>()
            .OrderBy(t => t.Name)];
    }

    private IReadOnlyDictionary<string, string> LoadPartials()
    {
        var sharedDir = Path.Combine(TemplatesDir.Value, "shared");
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
    public AgentTemplate? GetTemplate(string slug)
    {
        if (!AgentSlug.TryParse(slug, out _)) return null;
        var jsonPath = TemplatesDir.SafeTemplateFile(slug, ".json");
        return jsonPath != null && File.Exists(jsonPath.Value) ? LoadTemplateFile(jsonPath.Value) : null;
    }

    /// <summary>
    /// Saves an agent template.
    /// </summary>
    public void SaveTemplate(AgentTemplate template)
    {
        var jsonPath = TemplatesDir.SafeTemplateFile(template.Slug, ".json");
        var mdPath = TemplatesDir.SafeTemplateFile(template.Slug, ".md");
        if (jsonPath == null || mdPath == null)
            throw new ArgumentException($"Invalid template slug: '{template.Slug}'");

        Directory.CreateDirectory(TemplatesDir.Value);

        if (!string.IsNullOrEmpty(template.Content))
            File.WriteAllText(mdPath.Value, template.Content);

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
    public void DeleteTemplate(string slug)
    {
        var jsonPath = TemplatesDir.SafeTemplateFile(slug, ".json");
        var mdPath = TemplatesDir.SafeTemplateFile(slug, ".md");

        if (jsonPath != null && File.Exists(jsonPath.Value)) File.Delete(jsonPath.Value);
        if (mdPath != null && File.Exists(mdPath.Value)) File.Delete(mdPath.Value);
    }
}
