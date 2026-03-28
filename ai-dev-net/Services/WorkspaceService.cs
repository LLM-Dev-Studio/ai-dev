namespace AiDevNet.Services;

public class WorkspaceService(WorkspacePaths paths)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public List<WorkspaceProject> ListProjects()
    {
        if (!File.Exists(paths.RegistryPath)) return [];

        try
        {
            var json = File.ReadAllText(paths.RegistryPath);
            var registry = JsonSerializer.Deserialize<WorkspaceRegistry>(json, JsonOptions);
            if (registry?.Projects == null) return [];

            var projects = new List<WorkspaceProject>();
            foreach (var entry in registry.Projects)
            {
                if (!ProjectSlug.TryParse(entry.Path, out var entrySlug)) continue;
                var projectJsonPath = paths.ProjectJsonPath(entrySlug);

                string? description = null;
                string? createdAt = null;

                if (File.Exists(projectJsonPath))
                {
                    try
                    {
                        var pJson = File.ReadAllText(projectJsonPath);
                        var pData = JsonSerializer.Deserialize<ProjectJson>(pJson, JsonOptions);
                        description = pData?.Description;
                        createdAt = pData?.CreatedAt;
                    }
                    catch { /* use defaults */ }
                }

                var agentsDir = paths.AgentsDir(entrySlug);
                var agentCount = Directory.Exists(agentsDir)
                    ? Directory.GetDirectories(agentsDir).Length
                    : 0;

                projects.Add(new()
                {
                    Slug = entry.Slug,
                    Name = entry.Name,
                    Description = description,
                    CreatedAt = createdAt,
                    AgentCount = agentCount,
                });
            }

            return projects;
        }
        catch { return []; }
    }

    /// <summary>
    /// Creates a new project folder structure and registers it in workspaces.json.
    /// Returns an error message if validation fails, null on success.
    /// </summary>
    public string? CreateProject(string slug, string name, string? description, string? codebasePath = null)
    {
        if (!ProjectSlug.TryParse(slug, out var projectSlug))
            return "Slug must contain only lowercase letters, digits, and hyphens, and cannot start or end with a hyphen.";

        var projectDir = paths.ProjectDir(projectSlug);
        if (Directory.Exists(projectDir))
            return $"A project with slug '{slug}' already exists.";

        try
        {
            // Create folder structure
            Directory.CreateDirectory(paths.AgentsDir(projectSlug));
            Directory.CreateDirectory(Path.GetDirectoryName(paths.BoardPath(projectSlug))!);
            Directory.CreateDirectory(paths.DecisionsPendingDir(projectSlug));
            Directory.CreateDirectory(paths.DecisionsResolvedDir(projectSlug));

            // Write project.json
            var meta = new Dictionary<string, object?>
            {
                ["slug"] = slug,
                ["name"] = name,
                ["description"] = description ?? string.Empty,
                ["createdAt"] = DateTime.UtcNow.ToString("o"),
                ["codebaseInitialized"] = false,
            };
            if (!string.IsNullOrWhiteSpace(codebasePath))
                meta["codebasePath"] = codebasePath;

            File.WriteAllText(
                paths.ProjectJsonPath(projectSlug),
                JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

            // Write default board
            var boardJson = new
            {
                columns = new[]
                {
                    new { id = "backlog",     title = "Backlog",     taskIds = Array.Empty<string>() },
                    new { id = "in-progress", title = "In Progress", taskIds = Array.Empty<string>() },
                    new { id = "review",      title = "Review",      taskIds = Array.Empty<string>() },
                    new { id = "done",        title = "Done",        taskIds = Array.Empty<string>() },
                },
                tasks = new { },
            };
            File.WriteAllText(
                paths.BoardPath(projectSlug),
                JsonSerializer.Serialize(boardJson, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

            // Register in workspaces.json
            WorkspaceRegistry registry;
            if (File.Exists(paths.RegistryPath))
            {
                var existing = File.ReadAllText(paths.RegistryPath);
                registry = JsonSerializer.Deserialize<WorkspaceRegistry>(existing, JsonOptions) ?? new WorkspaceRegistry();
            }
            else
            {
                registry = new();
            }

            registry.Projects.Add(new() { Slug = slug, Path = slug, Name = name });
            File.WriteAllText(paths.RegistryPath, JsonSerializer.Serialize(registry, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

            return null; // success
        }
        catch (Exception ex)
        {
            // Clean up partial directory on failure
            try { if (Directory.Exists(projectDir)) Directory.Delete(projectDir.Value, recursive: true); } catch { }
            return $"Failed to create project: {ex.Message}";
        }
    }

    public ProjectDetail? GetProject(ProjectSlug projectSlug)
    {
        var jsonPath = paths.ProjectJsonPath(projectSlug);
        if (!File.Exists(jsonPath)) return null;

        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(jsonPath), JsonOptions);
            if (raw == null) return null;
            return new ProjectDetail
            {
                Slug = projectSlug,
                Name = raw.TryGetValue("name", out var n) ? n.GetString() ?? projectSlug : projectSlug,
                Description = raw.TryGetValue("description", out var d) ? d.GetString() ?? string.Empty : string.Empty,
                CodebasePath = raw.TryGetValue("codebasePath", out var cp) ? cp.GetString() : null,
                CreatedAt = raw.TryGetValue("createdAt", out var ca) ? ca.GetString() : null,
            };
        }
        catch { return null; }
    }

    public string? UpdateProject(ProjectSlug projectSlug, string name, string? description, string? codebasePath)
    {
        var jsonPath = paths.ProjectJsonPath(projectSlug);
        if (!File.Exists(jsonPath)) return "Project not found.";

        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(jsonPath), JsonOptions)
                      ?? [];
            var merged = raw.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
            merged["name"] = name;
            merged["description"] = description ?? string.Empty;
            if (string.IsNullOrWhiteSpace(codebasePath))
                merged.Remove("codebasePath");
            else
                merged["codebasePath"] = codebasePath;

            File.WriteAllText(jsonPath, JsonSerializer.Serialize(merged,
                new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

            // Keep display name in sync in workspaces.json
            if (File.Exists(paths.RegistryPath))
            {
                var registry = JsonSerializer.Deserialize<WorkspaceRegistry>(File.ReadAllText(paths.RegistryPath), JsonOptions);
                var entry = registry?.Projects.FirstOrDefault(p => p.Slug == projectSlug);
                if (entry != null)
                {
                    entry.Name = name;
                    File.WriteAllText(paths.RegistryPath, JsonSerializer.Serialize(registry,
                        new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
                }
            }

            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }
}

public class WorkspaceRegistry
{
    [JsonPropertyName("projects")]
    public List<WorkspaceRegistryEntry> Projects { get; set; } = [];
}

public class WorkspaceRegistryEntry
{
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class WorkspaceProject
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CreatedAt { get; set; }
    public int AgentCount { get; set; }
}

public class ProjectDetail
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? CodebasePath { get; set; }
    public string? CreatedAt { get; set; }
}

file class ProjectJson
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }
}
