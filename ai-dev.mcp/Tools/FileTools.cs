using ModelContextProtocol.Server;

using System.ComponentModel;
using System.Text;

namespace AiDev.Mcp.Tools;

[McpServerToolType]
public static class FileTools
{
    [McpServerTool, Description(
        "Read a file within the workspace. Path is relative to the workspace root " +
        "(e.g. 'board/board.json', 'agents/dev-alex/inbox/msg.md'). " +
        "Also accepts absolute paths within the workspace.")]
    public static string ReadFile(
        PathValidator validator,
        AuditLog audit,
        [Description("Relative or absolute path to the file")] string path)
    {
        var resolved = ResolvePath(validator, path);
        var parms = new Dictionary<string, string?> { ["path"] = path };

        if (!File.Exists(resolved))
        {
            audit.Record("read_file", parms, "not_found");
            return $"File not found: {path}";
        }

        audit.Record("read_file", parms, "ok");
        return File.ReadAllText(resolved);
    }

    [McpServerTool, Description(
        "List files and subdirectories in a directory within the workspace. " +
        "Path is relative to the workspace root.")]
    public static string ListDirectory(
        PathValidator validator,
        AuditLog audit,
        [Description("Relative or absolute path to the directory")] string path)
    {
        var resolved = ResolvePath(validator, path);
        var parms = new Dictionary<string, string?> { ["path"] = path };

        if (!Directory.Exists(resolved))
        {
            audit.Record("list_directory", parms, "not_found");
            return $"Directory not found: {path}";
        }

        var sb = new StringBuilder();
        foreach (var dir in Directory.GetDirectories(resolved).Order())
            sb.AppendLine($"[dir]  {Path.GetFileName(dir)}/");
        foreach (var file in Directory.GetFiles(resolved).Order())
            sb.AppendLine($"[file] {Path.GetFileName(file)}");

        audit.Record("list_directory", parms, "ok");
        return sb.Length > 0 ? sb.ToString() : "(empty directory)";
    }

    private static string ResolvePath(PathValidator validator, string path)
    {
        // Support both relative and absolute paths — both must resolve within workspace
        if (Path.IsPathRooted(path))
            return validator.ValidateAbsolute(path);
        return validator.Resolve(path);
    }
}
