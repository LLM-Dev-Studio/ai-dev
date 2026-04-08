using System.Text.Json;
using AiDev.Mcp;
using AiDev.Mcp.Tools;

namespace AiDev.Executors;

/// <summary>
/// Workspace-scoped filesystem tools for Ollama function-calling.
/// Directly delegates to the same exact tool methods exposed by the MCP server,
/// achieving complete uniformity of behavior between LLMs running by Ollama and Claude.
/// </summary>
public static class WorkspaceTools
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
        var validator = new PathValidator(workspaceRoot);
        var audit = new AuditLog(workspaceRoot);

        try
        {
            return toolName switch
            {
                ReadFile          => FileTools.ReadFile(validator, audit, RequireString(arguments, "path")),
                ListDirectory     => FileTools.ListDirectory(validator, audit, RequireString(arguments, "path")),
                UpdateBoard       => BoardTools.UpdateBoard(validator, audit, RequireString(arguments, "board_json")),
                UpdateAgentStatus => AgentTools.UpdateAgentStatus(validator, audit, 
                    RequireString(arguments, "agent_slug"), 
                    RequireString(arguments, "status"), 
                    OptionalString(arguments, "session_started_at"), 
                    OptionalInt(arguments, "pid")),
                WriteJournal      => AgentTools.WriteJournal(validator, audit, 
                    RequireString(arguments, "agent_slug"), 
                    RequireString(arguments, "date"), 
                    RequireString(arguments, "content")),
                WriteInbox        => MessageTools.WriteInbox(validator, audit, 
                    RequireString(arguments, "agent_slug"), 
                    RequireString(arguments, "filename"), 
                    RequireString(arguments, "content")),
                WriteOutbox       => MessageTools.WriteOutbox(validator, audit, 
                    RequireString(arguments, "agent_slug"), 
                    RequireString(arguments, "filename"), 
                    RequireString(arguments, "content")),
                WriteDecision     => DecisionTools.WriteDecision(validator, audit, 
                    RequireString(arguments, "filename"), 
                    RequireString(arguments, "content")),
                ReadKb            => KbTools.ReadKb(validator, audit, RequireString(arguments, "slug")),
                _                 => $"Unknown tool: {toolName}",
            };
        }
        catch (Exception ex)
        {
            return $"Error executing {toolName}: {ex.Message}";
        }
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
