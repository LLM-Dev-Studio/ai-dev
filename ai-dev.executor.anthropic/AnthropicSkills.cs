namespace AiDev.Executors;

/// <summary>
/// Defines the skills the Anthropic API executor can grant to agents.
///
/// Git operations are not listed here — the Anthropic executor talks directly to
/// the model API and cannot invoke Bash; git access is outside its scope.
/// If git tooling is needed, pair this executor with a tool-augmented approach
/// or use the Claude CLI executor instead.
/// </summary>
public static class AnthropicSkills
{
    public static readonly ExecutorSkill McpWorkspace = new(
        Key: "mcp-workspace",
        DisplayName: "Workspace tools",
        Description: "Grants read/write access to board, inbox, journal, KB, and decision files via direct filesystem calls.",
        DefaultEnabled: true);

    public static readonly IReadOnlyList<ExecutorSkill> All = [McpWorkspace];

    /// <summary>
    /// Resolves the effective skill set. If enabledSkills is empty, returns all DefaultEnabled skills;
    /// otherwise honours exactly the specified keys.
    /// </summary>
    internal static IReadOnlyList<ExecutorSkill> Resolve(IReadOnlyList<string> enabledSkills)
    {
        if (enabledSkills.Count == 0)
            return All.Where(s => s.DefaultEnabled).ToList();

        var keys = enabledSkills.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return All.Where(s => keys.Contains(s.Key)).ToList();
    }
}
