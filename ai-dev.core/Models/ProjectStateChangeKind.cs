namespace AiDev.Models;

[Flags]
public enum ProjectStateChangeKind
{
    None = 0,
    Messages = 1 << 0,
    Decisions = 1 << 1,
    Board = 1 << 2,
    Agents = 1 << 3,
}
