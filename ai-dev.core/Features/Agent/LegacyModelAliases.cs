namespace AiDev.Features.Agent;

/// <summary>
/// Maps old free-text model aliases (stored in agent.json before the model-registry refactor)
/// to their canonical executor + model-ID pairs.
/// Used by both AgentService (migration-on-load) and AgentRunnerService (runtime normalization).
/// </summary>
internal static class LegacyModelAliases
{
    /// <summary>
    /// Key: old alias (case-insensitive).
    /// Value: (ExecutorName, CanonicalModelId) — resolution is only valid when the agent's
    /// stored executor matches ExecutorName.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, (AgentExecutorName ExecutorName, string ModelId)> Map =
        new Dictionary<string, (AgentExecutorName, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["sonnet"] = (AgentExecutorName.Claude, "claude-sonnet-4-6"),
            ["opus"]   = (AgentExecutorName.Claude, "claude-opus-4-6"),
            ["haiku"]  = (AgentExecutorName.Claude, "claude-haiku-4-5-20251001"),
        };

    /// <summary>
    /// Returns the canonical model ID for <paramref name="alias"/> on <paramref name="executorName"/>,
    /// or null if the alias is unknown or belongs to a different executor.
    /// </summary>
    internal static string? Resolve(string alias, AgentExecutorName executorName)
    {
        if (Map.TryGetValue(alias, out var entry) && entry.ExecutorName == executorName)
            return entry.ModelId;
        return null;
    }
}
