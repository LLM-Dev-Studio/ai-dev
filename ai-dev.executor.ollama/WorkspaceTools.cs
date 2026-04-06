using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiDev.Executors;

/// <summary>
/// Workspace-scoped filesystem tools for Ollama function-calling.
/// Mirrors the MCP server tools but invoked directly — no MCP protocol overhead.
/// Called by OllamaAgentExecutor when the model returns tool_calls.
/// </summary>
internal static class WorkspaceTools
{
    // Tool name constants must match the function names declared in OllamaToolSchemas.
    public const string ReadFile          = "read_file";
    public const string ListDirectory     = "list_directory";
    public const string UpdateBoard       = "update_board";
    public const string UpdateAgentStatus = "update_agent_status";
    public const string WriteJournal      = "write_journal";
    public const string WriteInbox        = "write_inbox";
    public const string WriteOutbox       = "write_outbox";
    public const string WriteDecision     = "write_decision";
    public const string ReadKb            = "read_kb";

    /// <summary>Dispatch a tool call by name and execute it against the workspace.</summary>
    public static string Execute(string workspaceRoot, string toolName, JsonElement arguments)
    {
        try
        {
            return toolName switch
            {
                ReadFile          => DoReadFile(workspaceRoot, arguments),
                ListDirectory     => DoListDirectory(workspaceRoot, arguments),
                UpdateBoard       => DoUpdateBoard(workspaceRoot, arguments),
                UpdateAgentStatus => DoUpdateAgentStatus(workspaceRoot, arguments),
                WriteJournal      => DoWriteJournal(workspaceRoot, arguments),
                WriteInbox        => DoWriteInbox(workspaceRoot, arguments),
                WriteOutbox       => DoWriteOutbox(workspaceRoot, arguments),
                WriteDecision     => DoWriteDecision(workspaceRoot, arguments),
                ReadKb            => DoReadKb(workspaceRoot, arguments),
                _                 => $"Unknown tool: {toolName}",
            };
        }
        catch (Exception ex)
        {
            return $"Error executing {toolName}: {ex.Message}";
        }
    }

    // ── Path safety ────────────────────────────────────────────────────────────

    private static string Resolve(string workspaceRoot, string relativePath)
    {
        var root = EnsureTrailingSep(Path.GetFullPath(workspaceRoot));
        var full = Path.GetFullPath(Path.Combine(root.TrimEnd(Path.DirectorySeparatorChar), relativePath));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Path escapes workspace: {relativePath}");
        return full;
    }

    private static string ValidateAbsolute(string workspaceRoot, string absolutePath)
    {
        var root = EnsureTrailingSep(Path.GetFullPath(workspaceRoot));
        var full = Path.GetFullPath(absolutePath);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Path escapes workspace: {absolutePath}");
        return full;
    }

    private static void ValidateSlug(string slug, string label)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new InvalidOperationException($"{label} is required.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(slug, @"^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]$"))
            throw new InvalidOperationException($"Invalid {label}: '{slug}'. Use lowercase letters, digits, and hyphens.");
    }

    private static void ValidateFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            throw new InvalidOperationException("Filename is required.");
        if (filename.Contains("..") || filename.Contains('/') || filename.Contains('\\'))
            throw new InvalidOperationException($"Invalid filename: '{filename}'. Must not contain path separators or '..'.");
    }

    private static string EnsureTrailingSep(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    // ── Tool implementations ───────────────────────────────────────────────────

    private static string DoReadFile(string workspaceRoot, JsonElement args)
    {
        var path     = RequireString(args, "path");
        var resolved = Path.IsPathRooted(path)
            ? ValidateAbsolute(workspaceRoot, path)
            : Resolve(workspaceRoot, path);

        return File.Exists(resolved) ? File.ReadAllText(resolved) : $"File not found: {path}";
    }

    private static string DoListDirectory(string workspaceRoot, JsonElement args)
    {
        var path     = RequireString(args, "path");
        var resolved = Path.IsPathRooted(path)
            ? ValidateAbsolute(workspaceRoot, path)
            : Resolve(workspaceRoot, path);

        if (!Directory.Exists(resolved))
            return $"Directory not found: {path}";

        var sb = new StringBuilder();
        foreach (var dir in Directory.GetDirectories(resolved).Order())
            sb.AppendLine($"[dir]  {Path.GetFileName(dir)}/");
        foreach (var file in Directory.GetFiles(resolved).Order())
            sb.AppendLine($"[file] {Path.GetFileName(file)}");

        return sb.Length > 0 ? sb.ToString().TrimEnd() : "(empty directory)";
    }

    private static readonly JsonSerializerOptions BoardWriteOptions = new()
    {
        WriteIndented        = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string DoUpdateBoard(string workspaceRoot, JsonElement args)
    {
        var boardJson = RequireString(args, "board_json");
        var boardPath = Resolve(workspaceRoot, Path.Combine("board", "board.json"));

        JsonDocument doc;
        try { doc = JsonDocument.Parse(boardJson); }
        catch (JsonException ex) { return $"Invalid JSON: {ex.Message}"; }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("columns", out _) ||
                !root.TryGetProperty("tasks", out _))
                return "Board JSON must have 'columns' and 'tasks' properties.";

            Directory.CreateDirectory(Path.GetDirectoryName(boardPath)!);
            File.WriteAllText(boardPath, JsonSerializer.Serialize(root, BoardWriteOptions));
        }

        return "Board updated successfully.";
    }

    private static string DoUpdateAgentStatus(string workspaceRoot, JsonElement args)
    {
        var agentSlug        = RequireString(args, "agent_slug");
        var status           = RequireString(args, "status");
        var sessionStartedAt = OptionalString(args, "session_started_at");
        var pid              = OptionalInt(args, "pid");

        ValidateSlug(agentSlug, "agent_slug");

        if (status is not ("idle" or "running" or "error"))
            return $"Invalid status: '{status}'. Must be 'idle', 'running', or 'error'.";

        var agentJsonPath = Resolve(workspaceRoot, Path.Combine("agents", agentSlug, "agent.json"));
        if (!File.Exists(agentJsonPath))
            return $"agent.json not found for: {agentSlug}";

        var node = JsonNode.Parse(File.ReadAllText(agentJsonPath))!.AsObject();
        node["status"]           = status;
        node["sessionStartedAt"] = string.IsNullOrWhiteSpace(sessionStartedAt) ? null : sessionStartedAt;
        node["pid"]              = pid.HasValue ? JsonValue.Create(pid.Value) : null;

        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(agentJsonPath, node.ToJsonString(opts));
        return $"Agent '{agentSlug}' status updated to '{status}'.";
    }

    private static string DoWriteJournal(string workspaceRoot, JsonElement args)
    {
        var agentSlug = RequireString(args, "agent_slug");
        var date      = RequireString(args, "date");
        var content   = RequireString(args, "content");

        ValidateSlug(agentSlug, "agent_slug");

        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out _))
            return $"Invalid date: '{date}'. Use YYYY-MM-DD.";

        var journalDir  = Resolve(workspaceRoot, Path.Combine("agents", agentSlug, "journal"));
        Directory.CreateDirectory(journalDir);
        var journalPath = ValidateAbsolute(workspaceRoot, Path.Combine(journalDir, $"{date}.md"));
        File.AppendAllText(journalPath, content + Environment.NewLine);

        return $"Journal entry appended for {agentSlug} on {date}.";
    }

    private static string DoWriteInbox(string workspaceRoot, JsonElement args)
    {
        var agentSlug = RequireString(args, "agent_slug");
        var filename  = RequireString(args, "filename");
        var content   = RequireString(args, "content");

        ValidateSlug(agentSlug, "agent_slug");
        ValidateFilename(filename);

        var agentDir = Resolve(workspaceRoot, Path.Combine("agents", agentSlug));
        if (!Directory.Exists(agentDir))
            return $"Agent directory does not exist: agents/{agentSlug}";

        var inboxDir = Path.Combine(agentDir, "inbox");
        Directory.CreateDirectory(inboxDir);
        var filePath = ValidateAbsolute(workspaceRoot, Path.Combine(inboxDir, filename));
        File.WriteAllText(filePath, content);
        return $"Message written to agents/{agentSlug}/inbox/{filename}";
    }

    private static string DoWriteOutbox(string workspaceRoot, JsonElement args)
    {
        var agentSlug = RequireString(args, "agent_slug");
        var filename  = RequireString(args, "filename");
        var content   = RequireString(args, "content");

        ValidateSlug(agentSlug, "agent_slug");
        ValidateFilename(filename);

        var outboxDir = Resolve(workspaceRoot, Path.Combine("agents", agentSlug, "outbox"));
        Directory.CreateDirectory(outboxDir);
        var filePath = ValidateAbsolute(workspaceRoot, Path.Combine(outboxDir, filename));
        File.WriteAllText(filePath, content);
        return $"Message written to agents/{agentSlug}/outbox/{filename}";
    }

    private static string DoWriteDecision(string workspaceRoot, JsonElement args)
    {
        var filename = RequireString(args, "filename");
        var content  = RequireString(args, "content");

        if (filename.Contains("..") || filename.Contains('/') || filename.Contains('\\'))
            return $"Invalid filename: '{filename}'.";

        var pendingDir = Resolve(workspaceRoot, Path.Combine("decisions", "pending"));
        Directory.CreateDirectory(pendingDir);
        var filePath = ValidateAbsolute(workspaceRoot, Path.Combine(pendingDir, filename));
        File.WriteAllText(filePath, content);
        return $"Decision written to decisions/pending/{filename}";
    }

    private static string DoReadKb(string workspaceRoot, JsonElement args)
    {
        var slug = RequireString(args, "slug");

        if (string.IsNullOrWhiteSpace(slug) || slug.Contains("..") || slug.Contains('/') || slug.Contains('\\'))
            return $"Invalid slug: '{slug}'.";

        var kbDir       = Resolve(workspaceRoot, "kb");
        var articlePath = ValidateAbsolute(workspaceRoot, Path.Combine(kbDir, $"{slug}.md"));
        return File.Exists(articlePath) ? File.ReadAllText(articlePath) : $"KB article not found: {slug}";
    }

    // ── Argument extraction helpers ────────────────────────────────────────────

    private static string RequireString(JsonElement args, string name)
    {
        if (args.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString()!;
        throw new InvalidOperationException($"Missing required argument: '{name}'");
    }

    private static string? OptionalString(JsonElement args, string name)
    {
        if (args.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }

    private static int? OptionalInt(JsonElement args, string name)
    {
        if (args.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number)
            return prop.GetInt32();
        return null;
    }
}
