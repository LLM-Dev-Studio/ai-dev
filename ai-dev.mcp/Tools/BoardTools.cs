using ModelContextProtocol.Server;

using System.ComponentModel;
using System.Text.Json;

namespace AiDev.Mcp.Tools;

[McpServerToolType]
public static class BoardTools
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [McpServerTool, Description(
        "Atomically update the board state. Accepts the complete board JSON. " +
        "The JSON is parsed and re-serialised to prevent malformed data. " +
        "The board file is at board/board.json in the workspace.")]
    public static string UpdateBoard(
        PathValidator validator,
        AuditLog audit,
        [Description("Complete board JSON content (must be valid JSON matching the board schema)")] string boardJson)
    {
        var boardPath = validator.Resolve(Path.Combine("board", "board.json"));
        var parms = new Dictionary<string, string?> { ["boardJson"] = "(board data)" };

        // Parse to validate JSON structure, then re-serialise to ensure clean output
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(boardJson);
        }
        catch (JsonException ex)
        {
            audit.Record("update_board", parms, "invalid_json");
            return $"Invalid JSON: {ex.Message}";
        }

        // Validate required top-level structure
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("columns", out _) ||
            !root.TryGetProperty("tasks", out _))
        {
            audit.Record("update_board", parms, "invalid_schema");
            return "Board JSON must be an object with 'columns' and 'tasks' properties.";
        }

        // Re-serialise through parse to ensure clean, indented output
        Directory.CreateDirectory(Path.GetDirectoryName(boardPath)!);
        var cleanJson = JsonSerializer.Serialize(doc.RootElement, WriteOptions);
        File.WriteAllText(boardPath, cleanJson);
        doc.Dispose();

        audit.Record("update_board", parms, "ok");
        return "Board updated successfully.";
    }
}
