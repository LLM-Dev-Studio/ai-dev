namespace AiDev.Executors;

/// <summary>
/// Describes a language model that an executor can run.
/// Executors declare their built-in models via <see cref="IAgentExecutor.KnownModels"/>;
/// dynamic executors (Ollama, GitHub Models) also discover models at health-check time.
/// </summary>
public sealed record ModelDescriptor(
    /// <summary>The model identifier sent to the API (e.g. "claude-sonnet-4-6", "openai/gpt-4o").</summary>
    string Id,

    /// <summary>Human-readable name shown in the UI (e.g. "Claude Sonnet 4.6").</summary>
    string DisplayName,

    /// <summary>Which executor handles this model.</summary>
    string ExecutorName,

    /// <summary>Capabilities this model supports.</summary>
    ModelCapabilities Capabilities = ModelCapabilities.Streaming | ModelCapabilities.ToolCalling,

    /// <summary>Maximum output tokens. 0 means unknown/unset.</summary>
    int MaxTokens = 0,

    /// <summary>Context window size in tokens. 0 means unknown/unset.</summary>
    int ContextWindow = 0,

    /// <summary>Cost per 1 million input tokens in USD. Null when pricing is unknown.</summary>
    decimal? InputCostPer1MTokens = null,

    /// <summary>Cost per 1 million output tokens in USD. Null when pricing is unknown.</summary>
    decimal? OutputCostPer1MTokens = null);

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
