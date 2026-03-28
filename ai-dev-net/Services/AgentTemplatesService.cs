namespace AiDevNet.Services;

public class AgentTemplatesService(WorkspacePaths paths)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    // Same slug rules as AgentService/WorkspaceService: lowercase letters, digits, hyphens only.
    private static readonly System.Text.RegularExpressions.Regex SlugRegex =
        new(@"^[a-z0-9][a-z0-9\-]*[a-z0-9]$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    private static bool IsValidSlug(string? slug) =>
        !string.IsNullOrWhiteSpace(slug) &&
        SlugRegex.IsMatch(slug) &&
        !slug.Contains("..") && !slug.Contains('/') && !slug.Contains('\\');

    public List<AgentTemplate> ListTemplates()
    {
        var dir = paths.AgentTemplatesDir;
        if (!Directory.Exists(dir)) return [];

        return Directory.GetFiles(dir, "*.json")
            .Select(LoadTemplateFile)
            .OfType<AgentTemplate>()
            .OrderBy(t => t.Name)
            .ToList();
    }

    private AgentTemplate? LoadTemplateFile(string jsonPath)
    {
        try
        {
            var json = File.ReadAllText(jsonPath);
            var t = JsonSerializer.Deserialize<AgentTemplate>(json, JsonOptions);
            if (t is null) return null;

            var mdPath = Path.ChangeExtension(jsonPath, ".md");
            if (File.Exists(mdPath))
                t.Content = File.ReadAllText(mdPath);

            return t;
        }
        catch { return null; }
    }

    public AgentTemplate? GetTemplate(string slug)
    {
        if (!IsValidSlug(slug)) return null;
        var jsonPath = paths.SafeTemplatePath(slug, ".json");
        return jsonPath != null && File.Exists(jsonPath.Value) ? LoadTemplateFile(jsonPath.Value) : null;
    }

    public void SaveTemplate(AgentTemplate template)
    {
        var jsonPath = paths.SafeTemplatePath(template.Slug, ".json");
        var mdPath   = paths.SafeTemplatePath(template.Slug, ".md");
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
            Description = template.Description,
            Content = "",
        };
        File.WriteAllText(jsonPath.Value, JsonSerializer.Serialize(meta, JsonOptions));
    }

    public AgentTemplate CreateTemplate(AgentTemplate template)
    {
        if (string.IsNullOrEmpty(template.Model))
            template.Model = "sonnet";

        SaveTemplate(template);
        return template;
    }

    public void DeleteTemplate(string slug)
    {
        var jsonPath = paths.SafeTemplatePath(slug, ".json");
        var mdPath   = paths.SafeTemplatePath(slug, ".md");

        if (jsonPath != null && File.Exists(jsonPath.Value)) File.Delete(jsonPath.Value);
        if (mdPath   != null && File.Exists(mdPath.Value))   File.Delete(mdPath.Value);
    }
}
