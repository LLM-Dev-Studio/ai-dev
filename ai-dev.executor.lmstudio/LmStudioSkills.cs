namespace AiDev.Executors;

/// <summary>
/// Defines the skills the LM Studio executor can grant to agents.
/// </summary>
public static class LmStudioSkills
{
    public static readonly ExecutorSkill McpWorkspace = new(
        Key:            "mcp-workspace",
        DisplayName:    "Workspace tools",
        Description:    "Grants read/write access to board, inbox, journal, KB, and decision files via direct filesystem calls.",
        DefaultEnabled: true);

    public static readonly IReadOnlyList<ExecutorSkill> All = [McpWorkspace];

    internal static bool AreWorkspaceToolsEnabled(IReadOnlyList<string> enabledSkills) =>
        enabledSkills.Count == 0
            ? McpWorkspace.DefaultEnabled
            : enabledSkills.Contains(McpWorkspace.Key, StringComparer.OrdinalIgnoreCase);
}
