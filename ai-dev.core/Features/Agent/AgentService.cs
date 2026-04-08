using AiDev.Executors;
using AiDev.Services;

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
    public string? Executor { get; init; }
    public string[]? Skills { get; init; }
    public string? LastError { get; init; }
    public string? LastErrorAt { get; init; }
}

public class AgentService(
    WorkspacePaths paths,
    StudioSettingsService settings,
    AgentTemplatesService templates,
    AtomicFileWriter fileWriter,
    ProjectMutationCoordinator coordinator)
{
    private static readonly DomainError InvalidAgentSlugError = new("AGENT_INVALID_SLUG", "Invalid agent slug.");
    private static readonly DomainError AgentNotFoundError = new("AGENT_NOT_FOUND", "Agent not found.");


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
            var data = JsonSerializer.Deserialize<AgentJson>(json, JsonDefaults.Read);
            if (data == null) return null;

            var inboxDir = paths.AgentInboxDir(projectSlug, agentSlug);
            var inboxCount = inboxDir.Exists() ? Directory.GetFiles(inboxDir, "*.md").Length : 0;

            return new(
                slug: data.Slug ?? agentSlug,
                name: data.Name ?? agentSlug,
                role: data.Role ?? string.Empty,
                description: data.Description ?? string.Empty,
                model: data.Model,
                status: AgentStatus.From(data.Status),
                lastRunAt: DateTime.TryParse(data.LastRunAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var lastRun) ? lastRun : null,
                inboxCount: inboxCount,
                executor: data.Executor,
                skills: data.Skills ?? [],
                lastError: data.LastError,
                lastErrorAt: DateTime.TryParse(data.LastErrorAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var lastErrorAt) ? lastErrorAt : null);
        }
        catch { return null; }
    }

    public Result<Unit> SaveAgentMeta(ProjectSlug projectSlug, AgentSlug agentSlug, string name, string description,
        string model, string executor, IReadOnlyList<string>? skills = null)
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
                updated["executor"] = executor;
                if (skills != null)
                    updated["skills"] = skills;
                else
                    updated.Remove("skills");
                fileWriter.WriteAllText(jsonPath, JsonSerializer.Serialize(updated, JsonDefaults.Write));
                return new Ok<Unit>(Unit.Value);
            }
            catch (JsonException ex) { return new Err<Unit>(new DomainError("AGENT_INVALID_METADATA", ex.Message)); }
            catch (IOException ex) { return new Err<Unit>(new DomainError("AGENT_IO_ERROR", ex.Message)); }
            catch (UnauthorizedAccessException ex) { return new Err<Unit>(new DomainError("AGENT_IO_ERROR", ex.Message)); }
        });
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

                var agentJson = new
                {
                    slug = slug.Value,
                    name,
                    role = role ?? string.Empty,
                    model,
                    status = "idle",
                    description = description ?? string.Empty,
                };
                paths.AgentInboxDir(projectSlug, slug).Create();
                paths.AgentOutboxDir(projectSlug, slug).Create();
                paths.AgentJournalDir(projectSlug, slug).Create();
                fileWriter.WriteAllText(paths.AgentJsonPath(projectSlug, slug), JsonSerializer.Serialize(agentJson, JsonDefaults.Write));
                fileWriter.WriteAllText(paths.AgentClaudeMdPath(projectSlug, slug), claudeContent ?? $"# {name}\n\nYou are {name}.\n");

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
        if (!path.Exists()) return string.Empty;
        using var stream = new FileStream(path.Value, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public List<string> GetModelAliases() => [.. settings.GetSettings().Models.Keys];
}
