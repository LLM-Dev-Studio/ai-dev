using ModelContextProtocol.Server;

using System.ComponentModel;

namespace AiDev.Mcp.Tools;

[McpServerToolType]
public static class MessageTools
{
    [McpServerTool, Description(
        "Write a message file to another agent's inbox directory. " +
        "Validates that the target agent directory exists before writing.")]
    public static string WriteInbox(
        PathValidator validator,
        AuditLog audit,
        [Description("Project slug (e.g. 'demo-project')")] string projectSlug,
        [Description("Slug of the target agent (e.g. 'dev-alex')")] string agentSlug,
        [Description("Message filename (e.g. '20260402-090000-from-arch-nova.md')")] string filename,
        [Description("Full message content including YAML frontmatter")] string content)
    {
        PathValidator.ValidateSlug(projectSlug, "projectSlug");
        PathValidator.ValidateSlug(agentSlug, "agentSlug");
        ValidateFilename(filename);

        var agentDir = validator.ResolveProject(projectSlug, Path.Combine("agents", agentSlug));
        var parms = new Dictionary<string, string?>
        {
            ["projectSlug"] = projectSlug,
            ["agentSlug"] = agentSlug,
            ["filename"] = filename
        };

        if (!Directory.Exists(agentDir))
        {
            audit.Record("write_inbox", parms, "agent_not_found");
            return $"Agent directory does not exist: {projectSlug}/agents/{agentSlug}";
        }

        var inboxDir = Path.Combine(agentDir, "inbox");
        Directory.CreateDirectory(inboxDir);

        var filePath = validator.ValidateProjectAbsolute(projectSlug, Path.Combine(inboxDir, filename));
        File.WriteAllText(filePath, content);

        audit.Record("write_inbox", parms, "ok");
        return $"Message written to {projectSlug}/agents/{agentSlug}/inbox/{filename}";
    }

    [McpServerTool, Description(
        "Write a copy of a sent message to the calling agent's outbox directory.")]
    public static string WriteOutbox(
        PathValidator validator,
        AuditLog audit,
        [Description("Project slug (e.g. 'demo-project')")] string projectSlug,
        [Description("Slug of the sending agent (your own slug)")] string agentSlug,
        [Description("Message filename")] string filename,
        [Description("Full message content including YAML frontmatter")] string content)
    {
        PathValidator.ValidateSlug(projectSlug, "projectSlug");
        PathValidator.ValidateSlug(agentSlug, "agentSlug");
        ValidateFilename(filename);

        var outboxDir = validator.ResolveProject(projectSlug, Path.Combine("agents", agentSlug, "outbox"));
        Directory.CreateDirectory(outboxDir);

        var filePath = validator.ValidateProjectAbsolute(projectSlug, Path.Combine(outboxDir, filename));
        File.WriteAllText(filePath, content);

        var parms = new Dictionary<string, string?>
        {
            ["projectSlug"] = projectSlug,
            ["agentSlug"] = agentSlug,
            ["filename"] = filename
        };
        audit.Record("write_outbox", parms, "ok");
        return $"Message copied to {projectSlug}/agents/{agentSlug}/outbox/{filename}";
    }

    private static void ValidateFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            throw new InvalidOperationException("Filename is required.");
        if (filename.Contains("..") || filename.Contains('/') || filename.Contains('\\'))
            throw new InvalidOperationException($"Invalid filename: '{filename}'. Must not contain path separators or '..'.");
    }
}
