using ModelContextProtocol.Server;

using System.ComponentModel;

namespace AiDev.Mcp.Tools;

[McpServerToolType]
public static class KbTools
{
    [McpServerTool, Description(
        "Read a knowledge base article by slug. " +
        "Articles are markdown files in the kb/ directory of the target project.")]
    public static string ReadKb(
        PathValidator validator,
        AuditLog audit,
        [Description("Project slug (e.g. 'demo-project')")] string projectSlug,
        [Description("Article slug (filename without .md extension, e.g. 'agent-setup-guide')")] string slug)
    {
        PathValidator.ValidateSlug(projectSlug, "projectSlug");
        if (string.IsNullOrWhiteSpace(slug))
            return "Slug is required.";
        if (slug.Contains("..") || slug.Contains('/') || slug.Contains('\\'))
            return $"Invalid slug: '{slug}'.";

        var kbDir = validator.ResolveProject(projectSlug, "kb");
        var articlePath = validator.ValidateProjectAbsolute(projectSlug, Path.Combine(kbDir, $"{slug}.md"));

        var parms = new Dictionary<string, string?>
        {
            ["projectSlug"] = projectSlug,
            ["slug"] = slug
        };

        if (!File.Exists(articlePath))
        {
            audit.Record("read_kb", parms, "not_found");
            return $"KB article not found in {projectSlug}: {slug}";
        }

        audit.Record("read_kb", parms, "ok");
        return File.ReadAllText(articlePath);
    }
}
