namespace AiDev.Executors;

/// <summary>
/// A named capability an executor can grant to an agent.
/// Skills are declared by the executor and configured per-agent in agent.json.
/// </summary>
/// <param name="Key">Stable key stored in agent.json (e.g. "git-read").</param>
/// <param name="DisplayName">Human-readable name shown in the UI.</param>
/// <param name="Description">Short description shown in the UI.</param>
/// <param name="DefaultEnabled">Whether this skill is active when no "skills" field exists in agent.json. Ensures existing agents keep their current behaviour after the upgrade.</param>
public sealed record ExecutorSkill(
    string Key,
    string DisplayName,
    string Description,
    bool DefaultEnabled);
