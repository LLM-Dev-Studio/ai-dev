namespace AiDev.Executors;

/// <summary>
/// Defines the skills the Claude CLI executor can grant to agents, and maps
/// each skill key to the corresponding --allowedTools arguments.
///
/// Skills are stored in agent.json as a string array. When the array is absent,
/// all DefaultEnabled skills apply (preserving behaviour for existing agents).
/// </summary>
public static class ClaudeSkills
{
    /// <summary>
    /// The MCP server name used in .claude/settings.json and in the wildcard allow rule.
    /// </summary>
    public const string McpServerName = "ads-workspace";

    /// <summary>
    /// Built-in Claude CLI tools that agents must not use directly when the
    /// MCP workspace skill is active. The same list is written to settings.json
    /// (permissions.deny) and passed as --disallowedTools at runtime.
    /// </summary>
    public static readonly IReadOnlyList<string> DeniedRawTools =
    [        
    ];

    // default denyTools: "Read", "Write", "Edit", "Bash", "Glob", "Grep", "NotebookRead", "NotebookEdit",

    public static readonly ExecutorSkill GitRead = new(
        Key: "git-read",
        DisplayName: "Git read-only",
        Description: "Allows git log, diff, and status commands.",
        DefaultEnabled: true);

    public static readonly ExecutorSkill GitWrite = new(
        Key: "git-write",
        DisplayName: "Git write",
        Description: "Allows git add and commit commands.",
        DefaultEnabled: true);

    public static readonly ExecutorSkill McpWorkspace = new(
        Key: "mcp-workspace",
        DisplayName: "Workspace tools (MCP)",
        Description: "Grants access to workspace file, board, message, and decision tools via the MCP server.",
        DefaultEnabled: true);

    public static readonly IReadOnlyList<ExecutorSkill> All = [GitRead, GitWrite, McpWorkspace];

    /// <summary>
    /// Resolves the effective skill set: if enabledSkills is empty, returns all DefaultEnabled skills;
    /// otherwise honours exactly the specified keys.
    /// </summary>
    internal static HashSet<string> Resolve(IReadOnlyList<string> enabledSkills)
    {
        if (enabledSkills.Count == 0)
            return All.Where(s => s.DefaultEnabled).Select(s => s.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return enabledSkills.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Translates resolved skill keys into --allowedTools argument values for the Claude CLI.
    /// mcp-workspace has no entry here — it is granted by the MCP server registration in
    /// .claude/settings.json and needs no --allowedTools flag.
    /// </summary>
    internal static IEnumerable<string> ToAllowedTools(HashSet<string> skills)
    {
        if (skills.Contains("git-read"))
        {
            yield return "Bash(git log *)";
            yield return "Bash(git diff *)";
            yield return "Bash(git status)";
        }

        if (skills.Contains("git-write"))
        {
            yield return "Bash(git add *)";
            yield return "Bash(git commit *)";
        }
    }

    /// <summary>
    /// Translates resolved skill keys into --disallowedTools argument values for the Claude CLI.
    /// Used to enforce the use of MCP tools by removing built-in alternatives.
    /// </summary>
    internal static IEnumerable<string> ToDisallowedTools(HashSet<string> skills)
    {
        if (skills.Contains(McpWorkspace.Key))
        {
            foreach (var tool in DeniedRawTools)
                yield return tool;
        }
    }
}
