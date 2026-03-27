namespace AiDevNet.Services;

public class AgentTemplatesService(WorkspaceService workspace)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

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
        var dir = workspace.GetAgentTemplatesDir();
        var jsonPath = Path.Combine(dir, $"{slug}.json");
        return File.Exists(jsonPath) ? LoadTemplateFile(jsonPath) : null;
    }

    public void SaveTemplate(AgentTemplate template)
    {
        var dir = workspace.GetAgentTemplatesDir();
        Directory.CreateDirectory(dir);

        var jsonPath = Path.Combine(dir, $"{template.Slug}.json");
        var mdPath = Path.Combine(dir, $"{template.Slug}.md");

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
        var dir = workspace.GetAgentTemplatesDir();
        var jsonPath = Path.Combine(dir, $"{slug}.json");
        var mdPath = Path.Combine(dir, $"{slug}.md");

        if (File.Exists(jsonPath)) File.Delete(jsonPath);
        if (File.Exists(mdPath)) File.Delete(mdPath);
    }
}
