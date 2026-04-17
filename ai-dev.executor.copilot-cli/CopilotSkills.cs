namespace AiDev.Executors;

/// <summary>
/// Defines the skills the Copilot CLI executor can grant to agents, and maps
/// each skill key to the corresponding --allow-tool / --deny-tool arguments.
///
/// Copilot CLI requires --allow-all-tools in non-interactive mode (any tool
/// needing a permission prompt would otherwise hang the process), so skills
/// are enforced via explicit --deny-tool entries for the tools a disabled
/// skill would grant.
///
/// Skills are stored in agent.json as a string array. When the array is absent,
/// all DefaultEnabled skills apply (preserving behaviour for existing agents).
/// </summary>
public static class CopilotSkills
{
    /// <summary>
    /// The MCP server name used in .copilot/mcp-config.json and in the allow rule.
    /// Matches the Claude executor so agents can reuse the same server identifier.
    /// </summary>
    public const string McpServerName = "ads-workspace";

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
        DefaultEnabled: false);

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
    /// Translates resolved skill keys into --deny-tool argument values for the Copilot CLI.
    /// Because --allow-all-tools is required in non-interactive mode, anything we do NOT
    /// want the agent to touch must be explicitly denied.
    /// </summary>
    internal static IEnumerable<string> ToDeniedTools(HashSet<string> skills)
    {
        if (!skills.Contains(GitRead.Key))
        {
            yield return "shell(git log:*)";
            yield return "shell(git diff:*)";
            yield return "shell(git status)";
        }

        if (!skills.Contains(GitWrite.Key))
        {
            yield return "shell(git add:*)";
            yield return "shell(git commit:*)";
        }
    }
}
