namespace AiDev.Executors;

/// <summary>
/// A named capability an executor can grant to an agent.
/// Skills are declared by the executor and configured per-agent in agent.json.
/// </summary>
public sealed record ExecutorSkill(
    /// <summary>Stable key stored in agent.json (e.g. "git-read").</summary>
    string Key,

    /// <summary>Human-readable name shown in the UI.</summary>
    string DisplayName,

    /// <summary>Short description shown in the UI.</summary>
    string Description,

    /// <summary>
    /// Whether this skill is active when no "skills" field exists in agent.json.
    /// Ensures existing agents keep their current behaviour after the upgrade.
    /// </summary>
    bool DefaultEnabled);
