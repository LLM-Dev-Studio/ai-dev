namespace AiDev.Executors;

/// <summary>
/// Feature flags describing what a model can do.
/// New capabilities can be added without changing existing executor code.
/// </summary>
[Flags]
public enum ModelCapabilities
{
    None        = 0,

    /// <summary>Model supports streaming token output.</summary>
    Streaming   = 1 << 0,

    /// <summary>Model supports function/tool calling.</summary>
    ToolCalling = 1 << 1,

    /// <summary>Model accepts image inputs.</summary>
    Vision      = 1 << 2,

    /// <summary>Model supports extended reasoning / chain-of-thought.</summary>
    Reasoning   = 1 << 3,
}
