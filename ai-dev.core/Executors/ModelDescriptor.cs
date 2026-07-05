namespace AiDev.Executors;

/// <summary>
/// Describes a language model that an executor can run.
/// Executors declare their built-in models via <see cref="IAgentExecutor.KnownModels"/>;
/// dynamic executors (Ollama, GitHub Models) also discover models at health-check time.
/// </summary>
/// <param name="Capabilities">Capabilities this model supports.</param>
/// <param name="ContextWindow">Context window size in tokens. 0 means unknown/unset.</param>
/// <param name="DisplayName">Human-readable name shown in the UI (e.g. "Claude Sonnet 4.6").</param>
/// <param name="ExecutorName">Which executor handles this model.</param>
/// <param name="Id">The model identifier sent to the API (e.g. "claude-sonnet-4-6", "openai/gpt-4o").</param>
/// <param name="InputCostPer1MTokens">Cost per 1 million input tokens in USD. Null when pricing is unknown.</param>
/// <param name="MaxTokens">Maximum output tokens. 0 means unknown/unset.</param>
/// <param name="OutputCostPer1MTokens">Cost per 1 million output tokens in USD. Null when pricing is unknown.</param>
public sealed record ModelDescriptor(
    string Id,
    string DisplayName,
    AgentExecutorName ExecutorName,
    ModelCapabilities Capabilities = ModelCapabilities.Streaming | ModelCapabilities.ToolCalling,
    int MaxTokens = 0,
    int ContextWindow = 0,
    decimal? InputCostPer1MTokens = null,
    decimal? OutputCostPer1MTokens = null);
