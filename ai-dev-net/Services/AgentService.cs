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

public partial class AgentService(WorkspaceService workspace, StudioSettingsService settings, AgentTemplatesService templates)
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public List<AgentInfo> ListAgents(string projectSlug)
    {
        var agentsDir = Path.Combine(workspace.GetProjectPath(projectSlug), "agents");
        if (!Directory.Exists(agentsDir)) return [];

        return Directory.GetDirectories(agentsDir)
            .Select(d => LoadAgent(projectSlug, Path.GetFileName(d)))
            .OfType<AgentInfo>()
            .OrderBy(a => a.Name)
            .ToList();
    }

    public AgentInfo? LoadAgent(string projectSlug, string agentSlug)
    {
        var agentDir = Path.Combine(workspace.GetProjectPath(projectSlug), "agents", agentSlug);
        var jsonPath = Path.Combine(agentDir, "agent.json");
        if (!File.Exists(jsonPath)) return null;

        try
        {
            var json = File.ReadAllText(jsonPath);
            var data = JsonSerializer.Deserialize<AgentJson>(json, ReadOptions);
            if (data == null) return null;

            var inboxDir = Path.Combine(agentDir, "inbox");
            var inboxCount = Directory.Exists(inboxDir) ? Directory.GetFiles(inboxDir, "*.md").Length : 0;

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

    public string? SaveAgentMeta(string projectSlug, string agentSlug, string name, string description, string model)
    {
        if (!IsWithinAgentsDir(projectSlug, agentSlug, out var agentDir)) return "Invalid agent slug.";
        var jsonPath = Path.Combine(agentDir, "agent.json");
        if (!File.Exists(jsonPath)) return "Agent not found.";

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

    public string GetClaudeMd(string projectSlug, string agentSlug)
    {
        if (!IsWithinAgentsDir(projectSlug, agentSlug, out var agentDir)) return string.Empty;
        var path = Path.Combine(agentDir, "CLAUDE.md");
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    public string? SaveClaudeMd(string projectSlug, string agentSlug, string content)
    {
        if (!IsWithinAgentsDir(projectSlug, agentSlug, out var agentDir)) return "Invalid agent slug.";
        try
        {
            var path = Path.Combine(agentDir, "CLAUDE.md");
            File.WriteAllText(path, content);
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }

    public string? CreateAgent(string projectSlug, string agentSlug, string name, string? templateSlug)
    {
        if (string.IsNullOrWhiteSpace(agentSlug)) return "Slug is required.";
        if (agentSlug.Length > 1 && !AgentSlugString().IsMatch(agentSlug))
            return "Slug must contain only lowercase letters, digits, and hyphens.";
        if (agentSlug.Contains("..") || agentSlug.Contains('/') || agentSlug.Contains('\\'))
            return "Slug contains invalid characters.";

        var agentDir = Path.Combine(workspace.GetProjectPath(projectSlug), "agents", agentSlug);
        if (Directory.Exists(agentDir)) return $"Agent '{agentSlug}' already exists.";

        try
        {
            Directory.CreateDirectory(Path.Combine(agentDir, "inbox"));
            Directory.CreateDirectory(Path.Combine(agentDir, "outbox"));
            Directory.CreateDirectory(Path.Combine(agentDir, "journal"));

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
                slug = agentSlug,
                name,
                role = role ?? string.Empty,
                model,
                status = "idle",
                description = description ?? string.Empty,
            };
            File.WriteAllText(Path.Combine(agentDir, "agent.json"), JsonSerializer.Serialize(agentJson, WriteOptions));
            File.WriteAllText(Path.Combine(agentDir, "CLAUDE.md"), claudeContent ?? $"# {name}\n\nYou are {name}.\n");

            return null;
        }
        catch (Exception ex)
        {
            try { if (Directory.Exists(agentDir)) Directory.Delete(agentDir, true); }
            catch
            {
                // ignored
            }

            return ex.Message;
        }
    }

    public string[] ListTranscriptDates(string projectSlug, string agentSlug)
    {
        var dir = Path.Combine(workspace.GetProjectPath(projectSlug), "agents", agentSlug, "transcripts");
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir, "????-??-??.md")
            .Select(f => Path.GetFileNameWithoutExtension(f)!)
            .OrderDescending()
            .ToArray();
    }

    public string ReadTranscript(string projectSlug, string agentSlug, string date)
    {
        if (string.IsNullOrWhiteSpace(date)) return string.Empty;
        if (!IsWithinAgentsDir(projectSlug, agentSlug, out var agentDir)) return string.Empty;
        var transcriptsDir = Path.GetFullPath(Path.Combine(agentDir, "transcripts"));
        var path = Path.GetFullPath(Path.Combine(transcriptsDir, $"{date}.md"));
        if (!path.StartsWith(transcriptsDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        try { return File.Exists(path) ? File.ReadAllText(path) : string.Empty; }
        catch { return string.Empty; }
    }

    public List<string> GetModelAliases() => [.. settings.GetSettings().Models.Keys];

    /// <summary>
    /// Verifies that <paramref name="agentSlug"/> resolves inside the project's agents directory.
    /// Returns true and sets <paramref name="agentDir"/> on success; returns false on invalid input.
    /// </summary>
    private bool IsWithinAgentsDir(string projectSlug, string agentSlug, out string agentDir)
    {
        agentDir = string.Empty;
        var agentsDir = Path.GetFullPath(Path.Combine(workspace.GetProjectPath(projectSlug), "agents"));
        var resolved = Path.GetFullPath(Path.Combine(agentsDir, agentSlug));
        if (!resolved.StartsWith(agentsDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return false;
        agentDir = resolved;
        return true;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^[a-z0-9][a-z0-9\-]*[a-z0-9]$")]
    private static partial System.Text.RegularExpressions.Regex AgentSlugString();
}
