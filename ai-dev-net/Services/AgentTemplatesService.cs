namespace AiDevNet.Services;

public class AgentTemplatesService(WorkspaceService workspace)
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

    /// <summary>
    /// Returns the canonical path for a template file, or null if the slug would escape the templates directory.
    /// </summary>
    private string? SafeTemplatePath(string slug, string extension)
    {
        if (!IsValidSlug(slug)) return null;
        var dir = workspace.GetAgentTemplatesDir();
        var resolved = Path.GetFullPath(Path.Combine(dir, $"{slug}{extension}"));
        var canonicalDir = Path.GetFullPath(dir) + Path.DirectorySeparatorChar;
        return resolved.StartsWith(canonicalDir, StringComparison.OrdinalIgnoreCase) ? resolved : null;
    }

    public List<AgentTemplate> ListTemplates()
    {
        var dir = workspace.GetAgentTemplatesDir();
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
        var jsonPath = SafeTemplatePath(slug, ".json");
        return jsonPath != null && File.Exists(jsonPath) ? LoadTemplateFile(jsonPath) : null;
    }

    public void SaveTemplate(AgentTemplate template)
    {
        var jsonPath = SafeTemplatePath(template.Slug, ".json");
        var mdPath = SafeTemplatePath(template.Slug, ".md");
        if (jsonPath == null || mdPath == null)
            throw new ArgumentException($"Invalid template slug: '{template.Slug}'");

        var dir = workspace.GetAgentTemplatesDir();
        Directory.CreateDirectory(dir);

        if (!string.IsNullOrEmpty(template.Content))
            File.WriteAllText(mdPath, template.Content);

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
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(meta, JsonOptions));
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
        var jsonPath = SafeTemplatePath(slug, ".json");
        var mdPath = SafeTemplatePath(slug, ".md");

        if (jsonPath != null && File.Exists(jsonPath)) File.Delete(jsonPath);
        if (mdPath != null && File.Exists(mdPath)) File.Delete(mdPath);
    }
}
