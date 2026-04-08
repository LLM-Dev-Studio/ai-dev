using AiDev.Models;
using Microsoft.Extensions.Configuration;

namespace AiDev.Services;

public class StudioSettingsService(IConfiguration configuration)
{
    private const string StudioSettingsSectionName = "StudioSettings";
    private const string DefaultOllamaBaseUrl = "http://localhost:11434";

    private static readonly Dictionary<string, string> Defaults = new()
    {
        ["sonnet"] = "claude-sonnet-4-6",
        ["opus"] = "claude-opus-4-6",
        ["haiku"] = "claude-haiku-4-5-20251001",
    };

    public StudioSettings GetSettings()
    {
        var configuredModels = GetConfiguredModels();
        var models = new Dictionary<string, string>(Defaults);
        foreach (var (alias, modelId) in configuredModels)
        {
            if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(modelId))
                continue;

            models[alias] = modelId;
        }

        var ollamaBaseUrl = GetConfiguredValue(nameof(StudioSettings.OllamaBaseUrl));
        var anthropicApiKey = GetConfiguredValue(nameof(StudioSettings.AnthropicApiKey));

        return new StudioSettings
        {
            Models = models,
            OllamaBaseUrl = string.IsNullOrWhiteSpace(ollamaBaseUrl) ? DefaultOllamaBaseUrl : ollamaBaseUrl,
            AnthropicApiKey = string.IsNullOrWhiteSpace(anthropicApiKey) ? null : anthropicApiKey,
        };
    }

    public void SaveSettings(StudioSettings settings)
        => throw new InvalidOperationException("Studio settings are loaded from application configuration and cannot be saved at runtime.");

    private Dictionary<string, string> GetConfiguredModels()
    {
        var modelsSection = configuration.GetSection(StudioSettingsSectionName).GetSection(nameof(StudioSettings.Models));
        if (!modelsSection.GetChildren().Any())
            modelsSection = configuration.GetSection(nameof(StudioSettings.Models));

        return modelsSection
            .GetChildren()
            .Where(child => !string.IsNullOrWhiteSpace(child.Key) && !string.IsNullOrWhiteSpace(child.Value))
            .ToDictionary(child => child.Key, child => child.Value!, StringComparer.OrdinalIgnoreCase);
    }

    private string? GetConfiguredValue(string key)
        => configuration.GetSection(StudioSettingsSectionName)[key] ?? configuration[key];
}
