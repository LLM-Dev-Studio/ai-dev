namespace AiDev.Core.Local.Contracts;

public enum LocalAgentRole
{
    /// <summary>First iteration — maps the objective and selects breadth-first tools.</summary>
    Planner,

    /// <summary>Evidence gathering — reads files and searches for specific targets.</summary>
    Researcher,

    /// <summary>Synthesis — draws conclusions from gathered evidence.</summary>
    Coder,
}
