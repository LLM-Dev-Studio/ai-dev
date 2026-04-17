using Microsoft.Extensions.Configuration;

namespace AiDev.Services;

public class StudioSettingsService(IConfiguration configuration)
{
    private readonly string _settingsFilePath = Path.Combine(AppContext.BaseDirectory, FilePathConstants.StudioSettingsFileName);

    private const string StudioSettingsSectionName = "StudioSettings";
    private const string DefaultOllamaBaseUrl = "http://localhost:11434";
    private const string DefaultLmStudioBaseUrl = "http://localhost:1234";

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
        var lmStudioBaseUrl = GetConfiguredValue(nameof(StudioSettings.LmStudioBaseUrl));
        var anthropicApiKey = GetConfiguredValue(nameof(StudioSettings.AnthropicApiKey));
        var insightsExecutor = GetConfiguredValue(nameof(StudioSettings.InsightsExecutor));
        var insightsModel = GetConfiguredValue(nameof(StudioSettings.InsightsModel));

        return new StudioSettings
        {
            Models = models,
            OllamaBaseUrl = string.IsNullOrWhiteSpace(ollamaBaseUrl) ? DefaultOllamaBaseUrl : ollamaBaseUrl,
            LmStudioBaseUrl = string.IsNullOrWhiteSpace(lmStudioBaseUrl) ? DefaultLmStudioBaseUrl : lmStudioBaseUrl,
            AnthropicApiKey = string.IsNullOrWhiteSpace(anthropicApiKey) ? null : anthropicApiKey,
            InsightsExecutor = string.IsNullOrWhiteSpace(insightsExecutor) ? null : insightsExecutor.Trim(),
            InsightsModel = string.IsNullOrWhiteSpace(insightsModel) ? null : insightsModel.Trim(),
        };
    }

    public void SaveSettings(StudioSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var persisted = new StudioSettings
        {
            OllamaBaseUrl = string.IsNullOrWhiteSpace(settings.OllamaBaseUrl) ? DefaultOllamaBaseUrl : settings.OllamaBaseUrl.Trim(),
            LmStudioBaseUrl = string.IsNullOrWhiteSpace(settings.LmStudioBaseUrl) ? DefaultLmStudioBaseUrl : settings.LmStudioBaseUrl.Trim(),
            AnthropicApiKey = string.IsNullOrWhiteSpace(settings.AnthropicApiKey) ? null : settings.AnthropicApiKey.Trim(),
            GitHubToken = string.IsNullOrWhiteSpace(settings.GitHubToken) ? null : settings.GitHubToken.Trim(),
            InsightsExecutor = string.IsNullOrWhiteSpace(settings.InsightsExecutor) ? null : settings.InsightsExecutor.Trim(),
            InsightsModel = string.IsNullOrWhiteSpace(settings.InsightsModel) ? null : settings.InsightsModel.Trim(),
            Models = settings.Models is { Count: > 0 }
                ? new Dictionary<string, string>(settings.Models, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(),
        };

        var root = new Dictionary<string, object?>
        {
            [StudioSettingsSectionName] = persisted,
        };

        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
        File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(root, JsonDefaults.WriteIgnoreNull));
    }

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
