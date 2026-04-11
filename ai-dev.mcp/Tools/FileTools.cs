using ModelContextProtocol.Server;

using System.ComponentModel;
using System.Text;

namespace AiDev.Mcp.Tools;

[McpServerToolType]
public static class FileTools
{
    [McpServerTool, Description(
        "Read a file within a project. Path is relative to the project root " +
        "(e.g. 'board/board.json', 'agents/dev-alex/inbox/msg.md'). " +
        "Also accepts absolute paths within the target project.")]
    public static string ReadFile(
        PathValidator validator,
        AuditLog audit,
        [Description("Project slug (e.g. 'demo-project')")] string projectSlug,
        [Description("Relative or absolute path to the file")] string path)
    {
        PathValidator.ValidateSlug(projectSlug, "projectSlug");

        var resolved = ResolvePath(validator, projectSlug, path);
        var parms = new Dictionary<string, string?>
        {
            ["projectSlug"] = projectSlug,
            ["path"] = path
        };

        if (!File.Exists(resolved))
        {
            audit.Record("read_file", parms, "not_found");
            return $"File not found: {path}";
        }

        audit.Record("read_file", parms, "ok");
        return File.ReadAllText(resolved);
    }

    [McpServerTool, Description(
        "List files and subdirectories in a directory within a project. " +
        "Path is relative to the project root.")]
    public static string ListDirectory(
        PathValidator validator,
        AuditLog audit,
        [Description("Project slug (e.g. 'demo-project')")] string projectSlug,
        [Description("Relative or absolute path to the directory")] string path)
    {
        PathValidator.ValidateSlug(projectSlug, "projectSlug");

        var resolved = ResolvePath(validator, projectSlug, path);
        var parms = new Dictionary<string, string?>
        {
            ["projectSlug"] = projectSlug,
            ["path"] = path
        };

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

    private static string ResolvePath(PathValidator validator, string projectSlug, string path)
    {
        // Support both relative and absolute paths — both must resolve within the target project
        if (Path.IsPathRooted(path))
            return validator.ValidateProjectAbsolute(projectSlug, path);
        return validator.ResolveProject(projectSlug, path);
    }
}
