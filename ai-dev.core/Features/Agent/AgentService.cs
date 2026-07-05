using AiDev.Executors;

namespace AiDev.Features.Agent;

file class AgentJson
{
    public string? Slug { get; init; }
    public string? Name { get; init; }
    public string? Role { get; init; }
    public string? Model { get; init; }
    public string? Status { get; init; }
    public string? Description { get; init; }
    public string? LastRunAt { get; init; }
    public AgentExecutorName? Executor { get; init; }
    public string[]? Skills { get; init; }
    public string? LastError { get; init; }
    public string? LastErrorAt { get; init; }
    public string? Thinking { get; init; }
    public string? FailoverExecutor { get; init; }
    public string? FailedOverAt { get; init; }
}

/// <summary>
/// Provides agent discovery, persistence, and metadata management operations.
/// </summary>
public partial class AgentService(
    WorkspacePaths paths,
    AgentTemplatesService templates,
    AtomicFileWriter fileWriter,
    ProjectMutationCoordinator coordinator,
    IModelRegistry modelRegistry,
    ILogger<AgentService> logger)
{
    private static readonly DomainError InvalidAgentSlugError = new("AGENT_INVALID_SLUG", "Invalid agent slug.");
    private static readonly DomainError AgentNotFoundError = new("AGENT_NOT_FOUND", "Agent not found.");


    /// <summary>
    /// Lists agents for a project.
    /// </summary>
    /// <param name="projectSlug">The project whose agents should be listed.</param>
    /// <returns>The project agents ordered by name.</returns>
    public List<AgentInfo> ListAgents(ProjectSlug projectSlug)
    {
        var agentsDir = paths.AgentsDir(projectSlug);
        if (!agentsDir.Exists()) return [];

        return [.. Directory.GetDirectories(agentsDir)
            .Select(d => AgentSlug.TryParse(Path.GetFileName(d), out var a) ? LoadAgent(projectSlug, a) : null)
            .OfType<AgentInfo>()
            .OrderBy(a => a.Name)];
    }

    /// <summary>
    /// Loads agent metadata for a specific agent.
    /// </summary>
    /// <param name="projectSlug">The project that owns the agent.</param>
    /// <param name="agentSlug">The agent slug to load.</param>
    /// <returns>The loaded agent information, or <see langword="null"/> when unavailable.</returns>
    public AgentInfo? LoadAgent(ProjectSlug projectSlug, AgentSlug agentSlug)
    {
        var jsonPath = paths.AgentJsonPath(projectSlug, agentSlug);
        if (!jsonPath.Exists()) return null;

        try
        {
            var json = File.ReadAllText(jsonPath);
            var data = JsonSerializer.Deserialize<AgentJson>(json, JsonDefaults.Read);
            if (data == null) return null;

            var executor = data.Executor ?? AgentExecutorName.Default;
            var model = MigrateModelAlias(data.Model, executor, jsonPath);

            var inboxDir = paths.AgentInboxDir(projectSlug, agentSlug);
            var inboxCount = inboxDir.Exists() ? Directory.GetFiles(inboxDir, "*.md").Length : 0;

            var slug2       = data.Slug ?? agentSlug;
            var name        = data.Name ?? (string)agentSlug;
            var role        = data.Role ?? string.Empty;
            var description = data.Description ?? string.Empty;
            var model2      = string.IsNullOrWhiteSpace(model) ? "sonnet" : model;
            var thinking    = ThinkingLevel.Parse(data.Thinking);
            var skills      = data.Skills ?? [];

            var previousRunAt = DateTime.TryParse(data.LastRunAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var lastRun)
                ? lastRun : (DateTime?)null;
            var failure = data.LastError is { Length: > 0 } lastErrStr
                ? new AgentFailure(lastErrStr, DateTime.TryParse(data.LastErrorAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var lastErrAt) ? lastErrAt : DateTime.UtcNow)
                : null;
            var failover = AgentExecutorName.TryParse(data.FailoverExecutor, out var foExec)
                ? new AgentFailover(foExec, DateTime.TryParse(data.FailedOverAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var foAt) ? foAt : DateTime.UtcNow)
                : null;

            var status = AgentStatus.From(data.Status);
            return status switch
            {
                { IsRunning: true } => new AgentInfoRunning
                {
                    Slug = slug2, Name = name, Role = role, Description = description,
                    Model = model2, Executor = executor, Skills = skills,
                    ThinkingLevel = thinking, InboxCount = inboxCount, Failover = failover,
                    StartedAt = previousRunAt ?? DateTime.UtcNow,
                },
                { IsError: true } when failure is not null => new AgentInfoFailed
                {
                    Slug = slug2, Name = name, Role = role, Description = description,
                    Model = model2, Executor = executor, Skills = skills,
                    ThinkingLevel = thinking, InboxCount = inboxCount, Failover = failover,
                    Failure = failure, PreviousRunAt = previousRunAt,
                },
                _ => new AgentInfoIdle
                {
                    Slug = slug2, Name = name, Role = role, Description = description,
                    Model = model2, Executor = executor, Skills = skills,
                    ThinkingLevel = thinking, InboxCount = inboxCount, Failover = failover,
                    PreviousRunAt = previousRunAt,
                },
            };
        }
        catch (Exception ex)
        {
            LogFailedToLoadAgentJson(ex, projectSlug, agentSlug);
            return null;
        }
    }

    /// <summary>
    /// Resolves a stored model value to a real model ID.
    /// If the stored value is a known legacy alias for this executor (e.g. "sonnet" on Claude),
    /// rewrites agent.json in-place with the canonical ID.
    /// Returns the stored value unchanged when the alias belongs to a different executor
    /// or is already a real model ID.
    /// </summary>
    private string? MigrateModelAlias(string? storedModel, AgentExecutorName executor, AgentJsonFile jsonPath)
    {
        if (string.IsNullOrWhiteSpace(storedModel)) return storedModel;

        // Already a known real model ID — nothing to do.
        if (modelRegistry.Find(executor, storedModel) != null) return storedModel;

        // Try the deterministic legacy alias map, scoped to the correct executor.
        var resolved = LegacyModelAliases.Resolve(storedModel, executor);
        if (resolved != null)
        {
            PatchModelInJson(jsonPath, resolved);
            return resolved;
        }

        // Unknown value — return as-is (free-text model, different executor, or user override).
        return storedModel;
    }

    private void PatchModelInJson(AgentJsonFile jsonPath, string newModelId)
    {
        try
        {
            var json = File.ReadAllText(jsonPath);
            var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonDefaults.Read) ?? [];
            var updated = raw.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
            updated["model"] = newModelId;
            fileWriter.WriteAllText(jsonPath, JsonSerializer.Serialize(updated, JsonDefaults.Write));
        }
        catch { /* migration is best-effort */ }
    }

    public Result<Unit> SaveAgentMeta(ProjectSlug projectSlug, AgentSlug agentSlug, string name, string description,
        string model, AgentExecutorName executor, IReadOnlyList<string>? skills = null, ThinkingLevel thinkingLevel = default)
    {
        try { _ = paths.AgentDir(projectSlug, agentSlug); }
        catch (ArgumentException) { return new Err<Unit>(InvalidAgentSlugError); }

        return coordinator.Execute<Result<Unit>>(projectSlug, () =>
        {
            var jsonPath = paths.AgentJsonPath(projectSlug, agentSlug);
            if (!jsonPath.Exists()) return new Err<Unit>(AgentNotFoundError);

            try
            {
                var json = File.ReadAllText(jsonPath);
                var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonDefaults.Read) ?? [];
                var updated = raw.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
                updated["name"] = name;
                updated["description"] = description;
                updated["model"] = model;
                updated["executor"] = executor.Value;
                if (skills != null)
                    updated["skills"] = skills;
                else
                    updated.Remove("skills");
                if (thinkingLevel != ThinkingLevel.Off)
                    updated["thinking"] = thinkingLevel.Serialize();
                else
                    updated.Remove("thinking");
                fileWriter.WriteAllText(jsonPath, JsonSerializer.Serialize(updated, JsonDefaults.Write));
                return new Ok<Unit>(Unit.Value);
            }
            catch (JsonException ex) { return new Err<Unit>(new DomainError("AGENT_INVALID_METADATA", ex.Message)); }
            catch (IOException ex) { return new Err<Unit>(new DomainError("AGENT_IO_ERROR", ex.Message)); }
            catch (UnauthorizedAccessException ex) { return new Err<Unit>(new DomainError("AGENT_IO_ERROR", ex.Message)); }
        });
    }


    /// <summary>
    /// Persists a failover event for an agent. Writes failoverExecutor and failedOverAt to agent.json.
    /// Pass null executor to clear the failover state.
    /// </summary>
    public Result<Unit> SaveFailoverState(ProjectSlug projectSlug, AgentSlug agentSlug, AgentExecutorName? failoverExecutor, DateTime? failedOverAt)
    {
        try { _ = paths.AgentDir(projectSlug, agentSlug); }
        catch (ArgumentException) { return new Err<Unit>(InvalidAgentSlugError); }

        return coordinator.Execute<Result<Unit>>(projectSlug, () =>
        {
            var jsonPath = paths.AgentJsonPath(projectSlug, agentSlug);
            if (!jsonPath.Exists()) return new Err<Unit>(AgentNotFoundError);

            try
            {
                var json = File.ReadAllText(jsonPath);
                var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonDefaults.Read) ?? [];
                var updated = raw.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
                if (failoverExecutor != null)
                {
                    updated["failoverExecutor"] = failoverExecutor.Value;
                    updated["failedOverAt"] = (failedOverAt ?? DateTime.UtcNow).ToString("O");
                }
                else
                {
                    updated.Remove("failoverExecutor");
                    updated.Remove("failedOverAt");
                }
                fileWriter.WriteAllText(jsonPath, JsonSerializer.Serialize(updated, JsonDefaults.Write));
                return new Ok<Unit>(Unit.Value);
            }
            catch (JsonException ex) { return new Err<Unit>(new DomainError("AGENT_INVALID_METADATA", ex.Message)); }
            catch (IOException ex) { return new Err<Unit>(new DomainError("AGENT_IO_ERROR", ex.Message)); }
            catch (UnauthorizedAccessException ex) { return new Err<Unit>(new DomainError("AGENT_IO_ERROR", ex.Message)); }
        });
    }

/// <summary>
/// Gets the CLAUDE.md content for the specified agent.
/// </summary>
/// <param name="projectSlug">The slug of the project.</param>
/// <param name="agentSlug">The slug of the agent.</param>
/// <returns>The CLAUDE.md content, or an empty string when the file does not exist.</returns>
public string GetClaudeMd(ProjectSlug projectSlug, AgentSlug agentSlug)
    {
        try
        {
            var path = paths.AgentClaudeMdPath(projectSlug, agentSlug);
            return path.Exists() ? File.ReadAllText(path) : string.Empty;
        }
        catch (ArgumentException) { return string.Empty; }
    }

/// <summary>
/// Saves CLAUDE.md content for the specified agent.
/// </summary>
/// <param name="projectSlug">The slug of the project.</param>
/// <param name="agentSlug">The slug of the agent.</param>
/// <param name="content">The CLAUDE.md content to save.</param>
/// <returns>A result indicating success or failure.</returns>
public Result<Unit> SaveClaudeMd(ProjectSlug projectSlug, AgentSlug agentSlug, string content)
    {
        return coordinator.Execute<Result<Unit>>(projectSlug, () =>
        {
            try
            {
                fileWriter.WriteAllText(paths.AgentClaudeMdPath(projectSlug, agentSlug), content);
                return new Ok<Unit>(Unit.Value);
            }
            catch (ArgumentException) { return new Err<Unit>(InvalidAgentSlugError); }
            catch (IOException ex) { return new Err<Unit>(new DomainError("AGENT_IO_ERROR", ex.Message)); }
            catch (UnauthorizedAccessException ex) { return new Err<Unit>(new DomainError("AGENT_IO_ERROR", ex.Message)); }
        });
    }

/// <summary>
/// Creates a new agent from an optional template.
/// </summary>
/// <param name="projectSlug">The slug of the project.</param>
/// <param name="agentSlug">The slug of the agent.</param>
/// <param name="name">The name of the agent.</param>
/// <param name="templateSlug">The optional template slug used for the initial configuration.</param>
/// <returns>A result indicating success or failure.</returns>
public Result<Unit> CreateAgent(ProjectSlug projectSlug, string agentSlug, string name, string? templateSlug)
    {
        if (!AgentSlug.TryParse(agentSlug, out var slug))
            return new Err<Unit>(new DomainError("AGENT_SLUG_FORMAT", "Slug must contain only lowercase letters, digits, and hyphens, and cannot start or end with a hyphen."));

        var agentDir = paths.AgentDir(projectSlug, slug);
        if (agentDir.Exists()) return new Err<Unit>(new DomainError("AGENT_ALREADY_EXISTS", $"Agent '{agentSlug}' already exists."));

        return coordinator.Execute<Result<Unit>>(projectSlug, () =>
        {
            try
            {
                var resolvedSlug = !string.IsNullOrEmpty(templateSlug) ? templateSlug : "generic-standard";
                var tmpl = templates.GetTemplate(resolvedSlug);
                if (tmpl == null) return new Err<Unit>(new DomainError("AGENT_TEMPLATE_NOT_FOUND", $"Template '{resolvedSlug}' not found."));

                var role = tmpl.Role;
                var description = tmpl.Description;
                var model = tmpl.Model;
                var claudeContent = tmpl.Content;
                var executor = AgentExecutorName.TryParse(tmpl.Executor, out var tmplExecutor)
                    ? tmplExecutor
                    : AgentExecutorName.Default;

                // Only write skills when the template explicitly configures them.
                // Omitting the field means "use executor defaults" (DefaultEnabled skills are active).
                var agentJsonDict = new Dictionary<string, object?>
                {
                    ["slug"] = slug.Value,
                    ["name"] = name,
                    ["role"] = role ?? string.Empty,
                    ["model"] = model,
                    ["executor"] = executor.Value,
                    ["status"] = "idle",
                    ["description"] = description ?? string.Empty,
                };
                if (tmpl.Skills is { Count: > 0 } templateSkills)
                    agentJsonDict["skills"] = templateSkills;
                if (tmpl.ThinkingLevel != ThinkingLevel.Off)
                    agentJsonDict["thinking"] = tmpl.ThinkingLevel.Serialize();

                paths.AgentInboxDir(projectSlug, slug).Create();
                paths.AgentOutboxDir(projectSlug, slug).Create();
                paths.AgentJournalDir(projectSlug, slug).Create();
                fileWriter.WriteAllText(paths.AgentJsonPath(projectSlug, slug), JsonSerializer.Serialize(agentJsonDict, JsonDefaults.Write));
                fileWriter.WriteAllText(paths.AgentClaudeMdPath(projectSlug, slug), claudeContent ?? $"# {name}\n\nYou are {name}.\n");
                if (!string.IsNullOrEmpty(tmpl.CompactContent))
                    fileWriter.WriteAllText(Path.Combine(agentDir.Value, "CLAUDE.compact.md"), tmpl.CompactContent);

                return new Ok<Unit>(Unit.Value);
            }
            catch (IOException ex)
            {
                try { if (agentDir.Exists()) Directory.Delete(agentDir.Value, true); } catch { }
                return new Err<Unit>(new DomainError("AGENT_IO_ERROR", ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                try { if (agentDir.Exists()) Directory.Delete(agentDir.Value, true); } catch { }
                return new Err<Unit>(new DomainError("AGENT_IO_ERROR", ex.Message));
            }
        });
    }

/// <summary>
/// Lists available transcript dates for an agent.
/// </summary>
/// <param name="projectSlug">The slug of the project.</param>
/// <param name="agentSlug">The slug of the agent.</param>
/// <returns>The available transcript dates sorted in descending order.</returns>
public TranscriptDate[] ListTranscriptDates(ProjectSlug projectSlug, AgentSlug agentSlug)
    {
        try
        {
            var dir = paths.AgentTranscriptsDir(projectSlug, agentSlug);
            if (!dir.Exists()) return [];
            return [.. Directory.GetFiles(dir, "????-??-??.md")
                .Select(f => Path.GetFileNameWithoutExtension(f)!)
                .Select(s => TranscriptDate.TryParse(s, out var d) ? d : null)
                .OfType<TranscriptDate>()
                .OrderDescending()];
        }
        catch (ArgumentException) { return []; }
    }

/// <summary>
/// Reads transcript content for an agent on a specific date.
/// </summary>
/// <param name="projectSlug">The slug of the project.</param>
/// <param name="agentSlug">The slug of the agent.</param>
/// <param name="date">The transcript date.</param>
/// <returns>The transcript content, or an empty string when unavailable.</returns>
public string ReadTranscript(ProjectSlug projectSlug, AgentSlug agentSlug, TranscriptDate date)
    {
        var path = paths.TranscriptPath(projectSlug, agentSlug, date);
        if (!path.Exists()) return string.Empty;
        using var stream = new FileStream(path.Value, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Reads the AI-generated insight result for a session date.
    /// </summary>
    /// <param name="projectSlug">The slug of the project.</param>
    /// <param name="agentSlug">The slug of the agent.</param>
    /// <param name="date">The transcript date.</param>
    /// <returns>The insight result, or <see langword="null"/> when unavailable.</returns>
    public InsightResult? ReadInsights(ProjectSlug projectSlug, AgentSlug agentSlug, TranscriptDate date)
    {
        var path = paths.InsightPath(projectSlug, agentSlug, date);
        if (!path.Exists()) return null;
        try
        {
            var json = File.ReadAllText(path.Value);
            return System.Text.Json.JsonSerializer.Deserialize<InsightResult>(json, JsonDefaults.Read);
        }
        catch { return null; }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "[agent] Failed to load agent.json for {Project}/{Agent}")]
    private partial void LogFailedToLoadAgentJson(Exception ex, ProjectSlug project, AgentSlug agent);
}
