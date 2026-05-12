namespace AiDev.Models;

/// <summary>
/// Represents the categories of project state that can change.
/// </summary>
[Flags]
public enum ProjectStateChangeKind
{
    /// <summary>
    /// No state change.
    /// </summary>
    None = 0,

    /// <summary>
    /// Message-related state changed.
    /// </summary>
    Messages = 1 << 0,

    /// <summary>
    /// Decision-related state changed.
    /// </summary>
    Decisions = 1 << 1,

    /// <summary>
    /// Board-related state changed.
    /// </summary>
    Board = 1 << 2,

    /// <summary>
    /// Agent-related state changed.
    /// </summary>
    Agents = 1 << 3,
}
