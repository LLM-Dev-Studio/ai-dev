namespace AiDevNet.Services;

file class AgentJson
{
    [JsonPropertyName("slug")] public string? Slug { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("role")] public string? Role { get; init; }
    [JsonPropertyName("model")] public string? Model { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("lastRunAt")] public string? LastRunAt { get; init; }
    [JsonPropertyName("executor")] public string? Executor { get; init; }
}

public class AgentService(WorkspacePaths paths, StudioSettingsService settings, AgentTemplatesService templates)
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public List<AgentInfo> ListAgents(ProjectSlug projectSlug)
    {
        var agentsDir = paths.AgentsDir(projectSlug);
        if (!agentsDir.Exists()) return [];

        return Directory.GetDirectories(agentsDir)
            .Select(d => AgentSlug.TryParse(Path.GetFileName(d), out var a) ? LoadAgent(projectSlug, a) : null)
            .OfType<AgentInfo>()
            .OrderBy(a => a.Name)
            .ToList();
    }

    public AgentInfo? LoadAgent(ProjectSlug projectSlug, AgentSlug agentSlug)
    {
        var jsonPath = paths.AgentJsonPath(projectSlug, agentSlug);
        if (!jsonPath.Exists()) return null;

        try
        {
            var json = File.ReadAllText(jsonPath);
            var data = JsonSerializer.Deserialize<AgentJson>(json, ReadOptions);
            if (data == null) return null;

            var inboxDir = paths.AgentInboxDir(projectSlug, agentSlug);
            var inboxCount = inboxDir.Exists() ? Directory.GetFiles(inboxDir, "*.md").Length : 0;

            return new()
            {
                Slug = data.Slug ?? agentSlug,
                Name = data.Name ?? agentSlug,
                Role = data.Role ?? string.Empty,
                Model = data.Model ?? "sonnet",
                Status = data.Status ?? "idle",
                Description = data.Description ?? string.Empty,
                LastRunAt = data.LastRunAt,
                InboxCount = inboxCount,
                Executor = string.IsNullOrWhiteSpace(data.Executor) ? "claude" : data.Executor,
            };
        }
        catch { return null; }
    }

    public string? SaveAgentMeta(ProjectSlug projectSlug, AgentSlug agentSlug, string name, string description, string model)
    {
        try { _ = paths.AgentDir(projectSlug, agentSlug); }
        catch (ArgumentException) { return "Invalid agent slug."; }

        var jsonPath = paths.AgentJsonPath(projectSlug, agentSlug);
        if (!jsonPath.Exists()) return "Agent not found.";

        try
        {
            var json = File.ReadAllText(jsonPath);
            var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, ReadOptions) ?? [];
            var updated = raw.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
            updated["name"] = name;
            updated["description"] = description;
            updated["model"] = model;
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(updated, WriteOptions));
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }

    public string GetClaudeMd(ProjectSlug projectSlug, AgentSlug agentSlug)
    {
        try
        {
            var path = paths.AgentClaudeMdPath(projectSlug, agentSlug);
            return path.Exists() ? File.ReadAllText(path) : string.Empty;
        }
        catch (ArgumentException) { return string.Empty; }
    }

    public string? SaveClaudeMd(ProjectSlug projectSlug, AgentSlug agentSlug, string content)
    {
        try
        {
            File.WriteAllText(paths.AgentClaudeMdPath(projectSlug, agentSlug), content);
            return null;
        }
        catch (ArgumentException) { return "Invalid agent slug."; }
        catch (Exception ex) { return ex.Message; }
    }

    public string? CreateAgent(ProjectSlug projectSlug, string agentSlug, string name, string? templateSlug)
    {
        if (!AgentSlug.TryParse(agentSlug, out var slug))
            return "Slug must contain only lowercase letters, digits, and hyphens, and cannot start or end with a hyphen.";

        var agentDir = paths.AgentDir(projectSlug, slug);
        if (agentDir.Exists()) return $"Agent '{agentSlug}' already exists.";

        try
        {
            paths.AgentInboxDir(projectSlug, slug).Create();
            paths.AgentOutboxDir(projectSlug, slug).Create();
            paths.AgentJournalDir(projectSlug, slug).Create();

            string? role = null, description = null, model = "sonnet", claudeContent = null;

            if (!string.IsNullOrEmpty(templateSlug))
            {
                var tmpl = templates.GetTemplate(templateSlug);
                if (tmpl != null)
                {
                    role = tmpl.Role;
                    description = tmpl.Description;
                    model = tmpl.Model;
                    claudeContent = tmpl.Content;
                }
            }

            var agentJson = new
            {
                slug = slug.Value,
                name,
                role = role ?? string.Empty,
                model,
                status = "idle",
                description = description ?? string.Empty,
            };
            File.WriteAllText(paths.AgentJsonPath(projectSlug, slug), JsonSerializer.Serialize(agentJson, WriteOptions));
            File.WriteAllText(paths.AgentClaudeMdPath(projectSlug, slug), claudeContent ?? $"# {name}\n\nYou are {name}.\n");

            return null;
        }
        catch (Exception ex)
        {
            try { if (agentDir.Exists()) Directory.Delete(agentDir.Value, true); }
            catch
            {
                // ignored
            }

            return ex.Message;
        }
    }

    public TranscriptDate[] ListTranscriptDates(ProjectSlug projectSlug, AgentSlug agentSlug)
    {
        try
        {
            var dir = paths.AgentTranscriptsDir(projectSlug, agentSlug);
            if (!dir.Exists()) return [];
            return Directory.GetFiles(dir, "????-??-??.md")
                .Select(f => Path.GetFileNameWithoutExtension(f)!)
                .Select(s => TranscriptDate.TryParse(s, out var d) ? d : null)
                .OfType<TranscriptDate>()
                .OrderDescending()
                .ToArray();
        }
        catch (ArgumentException) { return []; }
    }

    public string ReadTranscript(ProjectSlug projectSlug, AgentSlug agentSlug, TranscriptDate date)
    {
        var path = paths.TranscriptPath(projectSlug, agentSlug, date);
        return path.Exists() ? File.ReadAllText(path.Value) : string.Empty;
    }

    public List<string> GetModelAliases() => [.. settings.GetSettings().Models.Keys];
}
