namespace AiDev.Features.Agent;

public class ModelResolver(StudioSettingsService settings)
{
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
