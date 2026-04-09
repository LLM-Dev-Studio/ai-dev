namespace AiDev.Features.Agent;

public class AgentTemplatesService(WorkspacePaths paths)
{
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
            var t = JsonSerializer.Deserialize<AgentTemplate>(json, JsonDefaults.Write);
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
        if (!AgentSlug.TryParse(slug, out _)) return null;
        var jsonPath = paths.SafeTemplatePath(slug, ".json");
        return jsonPath != null && File.Exists(jsonPath.Value) ? LoadTemplateFile(jsonPath.Value) : null;
    }

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

    public AgentTemplate CreateTemplate(AgentTemplate template)
    {
        if (string.IsNullOrEmpty(template.Model))
            template.Model = "claude-sonnet-4-6";

        SaveTemplate(template);
        return template;
    }

    public void DeleteTemplate(string slug)
    {
        var jsonPath = paths.SafeTemplatePath(slug, ".json");
        var mdPath = paths.SafeTemplatePath(slug, ".md");

        if (jsonPath != null && File.Exists(jsonPath.Value)) File.Delete(jsonPath.Value);
        if (mdPath != null && File.Exists(mdPath.Value)) File.Delete(mdPath.Value);
    }
}
