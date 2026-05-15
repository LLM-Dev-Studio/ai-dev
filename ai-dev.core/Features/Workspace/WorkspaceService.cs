namespace AiDev.Features.Workspace;

/// <summary>
/// Manages the global registry of known project codebases and per-project metadata operations.
/// </summary>
public class WorkspaceService
{
    private static readonly DomainError ProjectNotFoundError = new("WORKSPACE_NOT_FOUND", "Project not found.");

    private readonly WorkspacePaths _paths;
    private readonly AtomicFileWriter _fileWriter;
    private readonly ActiveWorkspaceHolder? _holder;
    private readonly string? _registryFilePath;
    private readonly ILogger<WorkspaceService>? _logger;

    private string EffectiveRegistryFile => _registryFilePath ?? GlobalPaths.ManagedProjectsFile;

    /// <summary>
    /// Initialises the service bound to a specific set of workspace paths.
    /// Suitable for direct construction in tests; pass <paramref name="registryFilePath"/>
    /// to redirect registry reads and writes away from the real managed-projects.json.
    /// </summary>
    public WorkspaceService(
        WorkspacePaths paths,
        AtomicFileWriter fileWriter,
        string? registryFilePath = null,
        ILogger<WorkspaceService>? logger = null)
    {
        _paths = paths;
        _fileWriter = fileWriter;
        _registryFilePath = registryFilePath;
        _logger = logger;
    }

    /// <summary>
    /// Initialises the service from the active workspace holder (used by the DI container).
    /// </summary>
    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public WorkspaceService(
        ActiveWorkspaceHolder workspace,
        AtomicFileWriter fileWriter,
        ILogger<WorkspaceService>? logger = null)
        : this(workspace.Paths, fileWriter, logger: logger)
    {
        _holder = workspace;
    }

    // ── Registry ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Lists all registered project codebases by reading each one's <c>.ai-dev/project.json</c>.
    /// The slug is read from the registry entry when available, falling back to parsing the file.
    /// </summary>
    public List<WorkspaceProject> ListProjects()
    {
        var registry = ReadRegistry();
        var projects = new List<WorkspaceProject>();

        foreach (var entry in registry.Projects)
        {
            try
            {
                var tempPaths = PathsFor(entry.Path);
                var projectJsonPath = tempPaths.ProjectJsonPath(default!);
                if (!File.Exists(projectJsonPath)) continue;

                var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    File.ReadAllText(projectJsonPath), JsonDefaults.Read);
                if (raw is null) continue;

                // Prefer the slug stored in the registry entry; fall back to reading from file
                // to support entries migrated from older registry files.
                var slugStr = entry.Slug ?? ReadString(raw, "projectSlug") ?? ReadString(raw, "slug");
                if (slugStr is null || !ProjectSlug.TryParse(slugStr, out var projectSlug)) continue;

                var name = ReadString(raw, "name") ?? slugStr;
                var description = ReadString(raw, "description");
                var createdAtStr = ReadString(raw, "createdAt");
                var createdAt = DateTime.TryParse(createdAtStr, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : (DateTime?)null;

                var agentsDir = tempPaths.AgentsDir(projectSlug);
                var agentCount = Directory.Exists(agentsDir) ? Directory.GetDirectories(agentsDir).Length : 0;

                projects.Add(new WorkspaceProject(projectSlug, name, description, createdAt, agentCount));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[workspace] Failed to read project at {Path} — skipping", entry.Path);
            }
        }

        return projects;
    }

    // ── Project lifecycle ─────────────────────────────────────────────────────

    /// <summary>
    /// Initialises a codebase as an AI Dev project at the active workspace root: creates the
    /// <c>.ai-dev/</c> structure, writes a <c>project.json</c>, and registers the path globally.
    /// </summary>
    public Result<Unit> CreateProject(string slug, string name, string? description = null, int apiPort = 0)
    {
        var codebasePath = Path.GetDirectoryName(_paths.Root.Value)
            ?? throw new InvalidOperationException(
                "Cannot derive codebase root from WorkspacePaths.Root — Root must be the .ai-dev/ directory.");
        return CreateProject(codebasePath, slug, name, description, apiPort);
    }

    /// <summary>
    /// Initialises a codebase as an AI Dev project: creates the <c>.ai-dev/</c> structure,
    /// writes a merged <c>project.json</c>, registers the path globally, and activates it.
    /// </summary>
    public Result<Unit> CreateProject(string codebasePath, string slug, string name,
        string? description = null, int apiPort = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new Err<Unit>(new DomainError("WORKSPACE_NAME_REQUIRED", "Name is required."));
        if (!ProjectSlug.TryParse(slug, out var projectSlug))
            return new Err<Unit>(new DomainError("WORKSPACE_INVALID_SLUG",
                "Slug must contain only lowercase letters, digits, and hyphens, and cannot start or end with a hyphen."));
        if (!Directory.Exists(codebasePath))
            return new Err<Unit>(new DomainError("WORKSPACE_INVALID_PATH",
                $"Directory not found: {codebasePath}"));

        var absolutePath = Path.GetFullPath(codebasePath);
        var tempPaths = PathsFor(absolutePath);

        try
        {
            // Create .ai-dev/ structure
            Directory.CreateDirectory(tempPaths.AgentsDir(projectSlug));
            Directory.CreateDirectory(Path.GetDirectoryName(tempPaths.BoardPath(projectSlug))!);
            Directory.CreateDirectory(tempPaths.DecisionsPendingDir(projectSlug));
            Directory.CreateDirectory(tempPaths.DecisionsResolvedDir(projectSlug));

            // Write merged project.json
            var meta = new Dictionary<string, object?>
            {
                ["projectSlug"] = slug,
                ["name"] = name,
                ["description"] = description ?? string.Empty,
                ["createdAt"] = DateTime.UtcNow.ToString("o"),
            };
            if (apiPort > 0) meta["apiPort"] = apiPort;

            _fileWriter.WriteAllText(
                tempPaths.ProjectJsonPath(projectSlug),
                JsonSerializer.Serialize(meta, JsonDefaults.Write));

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
            _fileWriter.WriteAllText(
                tempPaths.BoardPath(projectSlug),
                JsonSerializer.Serialize(boardJson, JsonDefaults.Write));

            // Register in managed-projects.json
            RegisterEntry(absolutePath, slug);

            // Activate via the holder when available (DI path)
            _holder?.Activate(absolutePath);

            return new Ok<Unit>(Unit.Value);
        }
        catch (IOException ex)
        {
            return new Err<Unit>(new DomainError("WORKSPACE_IO_ERROR", $"Failed to create project: {ex.Message}"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return new Err<Unit>(new DomainError("WORKSPACE_IO_ERROR", $"Failed to create project: {ex.Message}"));
        }
    }

    /// <summary>
    /// Registers an existing codebase (that already has <c>.ai-dev/project.json</c>) and activates it.
    /// </summary>
    public Result<Unit> RegisterProject(string codebasePath)
    {
        var absolutePath = Path.GetFullPath(codebasePath);
        var config = ProjectConfigReader.TryRead(absolutePath);
        if (config is null)
            return new Err<Unit>(new DomainError("WORKSPACE_INVALID_PATH",
                "No valid .ai-dev/project.json found at that path."));

        RegisterEntry(absolutePath, config.ProjectSlug);
        _holder?.Activate(absolutePath);
        return new Ok<Unit>(Unit.Value);
    }

    /// <summary>
    /// Removes a codebase from the global registry. Does not delete any files on disk.
    /// </summary>
    public void RemoveProject(string codebasePath)
    {
        var absolutePath = Path.GetFullPath(codebasePath);
        var registry = ReadRegistry();
        registry.Projects.RemoveAll(e =>
            string.Equals(Path.GetFullPath(e.Path), absolutePath, StringComparison.OrdinalIgnoreCase));
        if (string.Equals(registry.LastActivePath, absolutePath, StringComparison.OrdinalIgnoreCase))
            registry.LastActivePath = null;
        WriteRegistry(registry);
    }

    // ── Active project metadata ───────────────────────────────────────────────

    /// <summary>
    /// Gets detailed metadata for a project from the active workspace.
    /// </summary>
    public ProjectDetail? GetProject(ProjectSlug projectSlug)
    {
        var jsonPath = _paths.ProjectJsonPath(projectSlug);
        if (!File.Exists(jsonPath)) return null;

        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                File.ReadAllText(jsonPath), JsonDefaults.Read);
            if (raw is null) return null;

            var caStr = ReadString(raw, "createdAt");
            return new ProjectDetail
            {
                Slug = projectSlug,
                Name = ReadString(raw, "name") ?? projectSlug,
                Description = ReadString(raw, "description") ?? string.Empty,
                CreatedAt = DateTime.TryParse(caStr, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var caDate) ? caDate : null,
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[workspace] Failed to read project detail for {Slug}", projectSlug);
            return null;
        }
    }

    /// <summary>
    /// Updates editable metadata for the active project.
    /// </summary>
    public Result<Unit> UpdateProject(ProjectSlug projectSlug, string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new Err<Unit>(new DomainError("WORKSPACE_NAME_REQUIRED", "Name is required."));

        var jsonPath = _paths.ProjectJsonPath(projectSlug);
        if (!File.Exists(jsonPath)) return new Err<Unit>(ProjectNotFoundError);

        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                File.ReadAllText(jsonPath), JsonDefaults.Read) ?? [];
            var merged = raw.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
            merged["name"] = name;
            merged["description"] = description ?? string.Empty;

            _fileWriter.WriteAllText(jsonPath, JsonSerializer.Serialize(merged, JsonDefaults.Write));
            return new Ok<Unit>(Unit.Value);
        }
        catch (JsonException ex) { return new Err<Unit>(new DomainError("WORKSPACE_INVALID_METADATA", ex.Message)); }
        catch (IOException ex) { return new Err<Unit>(new DomainError("WORKSPACE_IO_ERROR", ex.Message)); }
        catch (UnauthorizedAccessException ex) { return new Err<Unit>(new DomainError("WORKSPACE_IO_ERROR", ex.Message)); }
    }

    // ── Registry helpers ──────────────────────────────────────────────────────

    internal WorkspaceRegistry ReadRegistry()
    {
        if (_registryFilePath is null) GlobalPaths.EnsureCreated();
        if (!File.Exists(EffectiveRegistryFile)) return new WorkspaceRegistry();
        try
        {
            var json = File.ReadAllText(EffectiveRegistryFile);
            return JsonSerializer.Deserialize<WorkspaceRegistry>(json, JsonDefaults.Read) ?? new WorkspaceRegistry();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[workspace] Failed to read managed-projects at {Path}", EffectiveRegistryFile);
            return new WorkspaceRegistry();
        }
    }

    private void WriteRegistry(WorkspaceRegistry registry)
    {
        if (_registryFilePath is null) GlobalPaths.EnsureCreated();
        else Directory.CreateDirectory(Path.GetDirectoryName(_registryFilePath)!);
        _fileWriter.WriteAllText(EffectiveRegistryFile, JsonSerializer.Serialize(registry, JsonDefaults.Write));
    }

    private void RegisterEntry(string absolutePath, string slug)
    {
        var registry = ReadRegistry();
        var existing = registry.Projects.FirstOrDefault(e =>
            string.Equals(Path.GetFullPath(e.Path), absolutePath, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            registry.Projects.Add(new WorkspaceRegistryEntry { Path = absolutePath, Slug = slug });
        }
        else
        {
            existing.Slug = slug;
        }
        registry.LastActivePath = absolutePath;
        WriteRegistry(registry);
    }

    private static WorkspacePaths PathsFor(string codebasePath) =>
        new(new RootDir(Path.Combine(codebasePath, ".ai-dev")));

    private static string? ReadString(Dictionary<string, JsonElement> raw, string key) =>
        raw.TryGetValue(key, out var el) ? el.GetString() : null;
}
