using ModelContextProtocol.Server;

using System.ComponentModel;

namespace AiDev.Mcp.Tools;

[McpServerToolType]
public static class DecisionTools
{
    [McpServerTool, Description(
        "Write a decision request file to the decisions/pending/ directory. " +
        "Used when an agent is blocked and needs a human to decide something.")]
    public static string WriteDecision(
        PathValidator validator,
        AuditLog audit,
        [Description("Decision filename (e.g. '20260402-090000-auth-middleware.md')")] string filename,
        [Description("Full decision content including YAML frontmatter")] string content)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return "Filename is required.";
        if (filename.Contains("..") || filename.Contains('/') || filename.Contains('\\'))
            return $"Invalid filename: '{filename}'.";

        var pendingDir = validator.Resolve(Path.Combine("decisions", "pending"));
        Directory.CreateDirectory(pendingDir);

        var filePath = validator.ValidateAbsolute(Path.Combine(pendingDir, filename));
        File.WriteAllText(filePath, content);

        var parms = new Dictionary<string, string?> { ["filename"] = filename };
        audit.Record("write_decision", parms, "ok");
        return $"Decision written to decisions/pending/{filename}";
    }
}
