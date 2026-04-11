using ModelContextProtocol.Server;

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiDev.Mcp.Tools;

[McpServerToolType]
public static class AgentTools
{
    [McpServerTool, Description("Update status fields of an agent's agent.json file. Only modifies status, sessionStartedAt, and pid — all other fields are preserved.")]
    public static string UpdateAgentStatus(
        PathValidator validator,
        AuditLog audit,
        [Description("Project slug (e.g. 'demo-project')")] string projectSlug,
        [Description("Agent slug (e.g. 'dev-alex')")] string agentSlug,
        [Description("New status: 'idle', 'running', or 'error'")] string status,
        [Description("ISO 8601 UTC timestamp for session start, or empty/null to clear")] string? sessionStartedAt = null,
        [Description("Process ID, or null to clear")] int? pid = null)
    {
        PathValidator.ValidateSlug(projectSlug, "projectSlug");
        PathValidator.ValidateSlug(agentSlug, "agentSlug");

        if (status is not ("idle" or "running" or "error"))
            return $"Invalid status: '{status}'. Must be 'idle', 'running', or 'error'.";

        var agentJsonPath = validator.ResolveProject(projectSlug, Path.Combine("agents", agentSlug, "agent.json"));
        var parms = new Dictionary<string, string?>
        {
            ["projectSlug"] = projectSlug,
            ["agentSlug"] = agentSlug,
            ["status"] = status
        };

        if (!File.Exists(agentJsonPath))
        {
            audit.Record("update_agent_status", parms, "not_found");
            return $"agent.json not found for agent: {projectSlug}/{agentSlug}";
        }

        var json = File.ReadAllText(agentJsonPath);
        var node = JsonNode.Parse(json)!.AsObject();

        node["status"] = status;
        node["sessionStartedAt"] = string.IsNullOrWhiteSpace(sessionStartedAt) ? null : sessionStartedAt;
        node["pid"] = pid.HasValue ? JsonValue.Create(pid.Value) : null;

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(agentJsonPath, node.ToJsonString(options));

        audit.Record("update_agent_status", parms, "ok");
        return $"Agent '{agentSlug}' status updated to '{status}'.";
    }

    [McpServerTool, Description(
        "Append to or create a daily journal entry for an agent. " +
        "Journal files live at agents/{slug}/journal/YYYY-MM-DD.md.")]
    public static string WriteJournal(
        PathValidator validator,
        AuditLog audit,
        [Description("Project slug (e.g. 'demo-project')")] string projectSlug,
        [Description("Agent slug")] string agentSlug,
        [Description("Date string in YYYY-MM-DD format")] string date,
        [Description("Markdown content to append to the journal")] string content)
    {
        PathValidator.ValidateSlug(projectSlug, "projectSlug");
        PathValidator.ValidateSlug(agentSlug, "agentSlug");

        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out _))
            return $"Invalid date format: '{date}'. Use YYYY-MM-DD.";

        var journalDir = validator.ResolveProject(projectSlug, Path.Combine("agents", agentSlug, "journal"));
        Directory.CreateDirectory(journalDir);

        // Filename is validated by the date format check above — no traversal possible
        var journalPath = validator.ValidateProjectAbsolute(projectSlug, Path.Combine(journalDir, $"{date}.md"));

        var parms = new Dictionary<string, string?>
        {
            ["projectSlug"] = projectSlug,
            ["agentSlug"] = agentSlug,
            ["date"] = date
        };

        // Append to existing file or create new
        File.AppendAllText(journalPath, content + Environment.NewLine);

        audit.Record("write_journal", parms, "ok");
        return $"Journal entry appended for {projectSlug}/{agentSlug} on {date}.";
    }
}
