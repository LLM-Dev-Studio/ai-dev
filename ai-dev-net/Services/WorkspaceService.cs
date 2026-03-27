namespace AiDevNet.Services;

public class WorkspaceService(IWebHostEnvironment env)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string GetWorkspaceRoot()
    {
        var envVar = Environment.GetEnvironmentVariable("WORKSPACE_ROOT");
        if (!string.IsNullOrEmpty(envVar))
        {
            // Reject relative paths and UNC paths (\\server\share) — must be an absolute local path.
            if (!Path.IsPathFullyQualified(envVar) || envVar.StartsWith(@"\\"))
                throw new InvalidOperationException(
                    $"WORKSPACE_ROOT must be an absolute local path, got: '{envVar}'");
            return envVar;
        }
        return Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "workspaces"));
    }

    public string GetStudioSettingsPath() =>
        Path.Combine(GetWorkspaceRoot(), "studio-settings.json");

    public string GetAgentTemplatesDir() =>
        Path.Combine(GetWorkspaceRoot(), "agent-templates");

    public string GetRegistryPath() =>
        Path.Combine(GetWorkspaceRoot(), "workspaces.json");

    public string GetProjectPath(string projectSlug)
    {
        var baseDir = GetWorkspaceRoot();
        var resolved = Path.GetFullPath(Path.Combine(baseDir, projectSlug));
        var canonicalBase = Path.GetFullPath(baseDir);
        // Reject any slug that resolves outside the workspace root (e.g. "../../etc/passwd").
        if (!resolved.StartsWith(canonicalBase + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(resolved, canonicalBase, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Invalid project slug: '{projectSlug}'", nameof(projectSlug));
        return resolved;
    }

    public List<WorkspaceProject> ListProjects()
    {
        var registryPath = GetRegistryPath();
        if (!File.Exists(registryPath)) return [];

        try
        {
            var json = File.ReadAllText(registryPath);
            var registry = JsonSerializer.Deserialize<WorkspaceRegistry>(json, JsonOptions);
            if (registry?.Projects == null) return [];

            var projects = new List<WorkspaceProject>();
            foreach (var entry in registry.Projects)
            {
                var projectDir = GetProjectPath(entry.Path);
                var projectJsonPath = Path.Combine(projectDir, "project.json");

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

                var agentsDir = Path.Combine(projectDir, "agents");
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
        // Validate slug — only lowercase letters, digits, hyphens; no path traversal
        if (string.IsNullOrWhiteSpace(slug))
            return "Slug is required.";
        if (!System.Text.RegularExpressions.Regex.IsMatch(slug, @"^[a-z0-9][a-z0-9\-]*[a-z0-9]$"))
            return "Slug must contain only lowercase letters, digits, and hyphens, and cannot start or end with a hyphen.";
        if (slug.Contains("..") || slug.Contains('/') || slug.Contains('\\'))
            return "Slug contains invalid characters.";

        var projectDir = GetProjectPath(slug);
        if (Directory.Exists(projectDir))
            return $"A project with slug '{slug}' already exists.";

        try
        {
            // Create folder structure
            Directory.CreateDirectory(Path.Combine(projectDir, "agents"));
            Directory.CreateDirectory(Path.Combine(projectDir, "board"));
            Directory.CreateDirectory(Path.Combine(projectDir, "decisions", "pending"));
            Directory.CreateDirectory(Path.Combine(projectDir, "decisions", "resolved"));

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
                Path.Combine(projectDir, "project.json"),
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
                Path.Combine(projectDir, "board", "board.json"),
                JsonSerializer.Serialize(boardJson, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

            // Register in workspaces.json
            var registryPath = GetRegistryPath();
            WorkspaceRegistry registry;
            if (File.Exists(registryPath))
            {
                var existing = File.ReadAllText(registryPath);
                registry = JsonSerializer.Deserialize<WorkspaceRegistry>(existing, JsonOptions) ?? new WorkspaceRegistry();
            }
            else
            {
                registry = new();
            }

            registry.Projects.Add(new() { Slug = slug, Path = slug, Name = name });
            File.WriteAllText(registryPath, JsonSerializer.Serialize(registry, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

            return null; // success
        }
        catch (Exception ex)
        {
            // Clean up partial directory on failure
            try { if (Directory.Exists(projectDir)) Directory.Delete(projectDir, recursive: true); } catch { }
            return $"Failed to create project: {ex.Message}";
        }
    }

    public ProjectDetail? GetProject(string projectSlug)
    {
        var projectDir = GetProjectPath(projectSlug);
        var jsonPath = Path.Combine(projectDir, "project.json");
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

    public string? UpdateProject(string projectSlug, string name, string? description, string? codebasePath)
    {
        var projectDir = GetProjectPath(projectSlug);
        var jsonPath = Path.Combine(projectDir, "project.json");
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
            var registryPath = GetRegistryPath();
            if (File.Exists(registryPath))
            {
                var registry = JsonSerializer.Deserialize<WorkspaceRegistry>(File.ReadAllText(registryPath), JsonOptions);
                var entry = registry?.Projects.FirstOrDefault(p => p.Slug == projectSlug);
                if (entry != null)
                {
                    entry.Name = name;
                    File.WriteAllText(registryPath, JsonSerializer.Serialize(registry,
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
