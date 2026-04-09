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
    internal static readonly IReadOnlyDictionary<string, (string ExecutorName, string ModelId)> Map =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["sonnet"] = ("claude", "claude-sonnet-4-6"),
            ["opus"]   = ("claude", "claude-opus-4-6"),
            ["haiku"]  = ("claude", "claude-haiku-4-5-20251001"),
        };

    /// <summary>
    /// Returns the canonical model ID for <paramref name="alias"/> on <paramref name="executorName"/>,
    /// or null if the alias is unknown or belongs to a different executor.
    /// </summary>
    internal static string? Resolve(string alias, string executorName)
    {
        if (Map.TryGetValue(alias, out var entry) &&
            string.Equals(entry.ExecutorName, executorName, StringComparison.OrdinalIgnoreCase))
            return entry.ModelId;
        return null;
    }
}
