namespace AiDev.Features.Agent;

/// <summary>
/// Resolves model aliases to concrete model identifiers.
/// </summary>
public class ModelResolver(StudioSettingsService settings)
{
    /// <summary>
    /// Resolves the provided model alias or identifier for the given executor.
    /// </summary>
    /// <param name="modelOrAlias">The configured model alias or raw model identifier.</param>
    /// <param name="executor">The executor for which the model should be resolved.</param>
    /// <returns>The resolved concrete model identifier, or the original value when no mapping exists.</returns>
    public string Resolve(string modelOrAlias, AgentExecutorName executor)
    {
        if (string.IsNullOrWhiteSpace(modelOrAlias))
            return modelOrAlias;

        var configuredModels = settings.GetSettings().Models;
        if (configuredModels.TryGetValue(modelOrAlias, out var configuredModelId)
            && !string.IsNullOrWhiteSpace(configuredModelId))
            return configuredModelId;

        return LegacyModelAliases.Resolve(modelOrAlias, executor) ?? modelOrAlias;
    }
}
