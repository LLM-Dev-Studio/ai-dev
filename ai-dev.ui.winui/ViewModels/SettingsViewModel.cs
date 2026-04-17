using AiDev.Executors;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly StudioSettingsService _settingsService;
    private readonly IModelRegistry _modelRegistry;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string AnthropicApiKey { get; set; } = "";

    [ObservableProperty]
    public partial string OllamaBaseUrl { get; set; } = "";

    [ObservableProperty]
    public partial string LmStudioBaseUrl { get; set; } = "";

    [ObservableProperty]
    public partial string GitHubToken { get; set; } = "";

    [ObservableProperty]
    public partial string InsightsExecutor { get; set; } = "";

    [ObservableProperty]
    public partial string InsightsModel { get; set; } = "";

    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    [ObservableProperty]
    public partial bool Saved { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = "";

    public ObservableCollection<string> AvailableInsightsExecutors { get; } = [];
    public ObservableCollection<string> AvailableInsightsModels { get; } = [];

    public SettingsViewModel(StudioSettingsService settingsService, IModelRegistry modelRegistry)
    {
        _settingsService = settingsService;
        _modelRegistry = modelRegistry;

        foreach (var executor in AgentExecutorName.Supported)
            AvailableInsightsExecutors.Add(executor.Value);
    }

    [RelayCommand]
    public Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var settings = _settingsService.GetSettings();
            AnthropicApiKey = settings.AnthropicApiKey ?? "";
            OllamaBaseUrl = settings.OllamaBaseUrl;
            LmStudioBaseUrl = settings.LmStudioBaseUrl;
            GitHubToken = settings.GitHubToken ?? "";
            InsightsExecutor = settings.InsightsExecutor ?? "";
            RefreshInsightsModels();

            var configuredModel = settings.InsightsModel ?? "";
            if (!string.IsNullOrWhiteSpace(configuredModel) && AvailableInsightsModels.Contains(configuredModel, StringComparer.OrdinalIgnoreCase))
                InsightsModel = configuredModel;
            else if (AvailableInsightsModels.Count > 0)
                InsightsModel = AvailableInsightsModels[0];
            else
                InsightsModel = configuredModel;

            Saved = false;
            ErrorMessage = "";
            return Task.CompletedTask;
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnInsightsExecutorChanged(string value)
    {
        RefreshInsightsModels();

        if (AvailableInsightsModels.Count == 0)
            return;

        if (!AvailableInsightsModels.Contains(InsightsModel, StringComparer.OrdinalIgnoreCase))
            InsightsModel = AvailableInsightsModels[0];
    }

    private void RefreshInsightsModels()
    {
        AvailableInsightsModels.Clear();
        if (string.IsNullOrWhiteSpace(InsightsExecutor))
            return;

        foreach (var model in _modelRegistry.GetModelsForExecutor(InsightsExecutor))
            AvailableInsightsModels.Add(model.Id);
    }

    [RelayCommand]
    public Task SaveAsync()
    {
        IsSaving = true;
        Saved = false;
        ErrorMessage = "";

        try
        {
            _settingsService.SaveSettings(new StudioSettings
            {
                AnthropicApiKey = string.IsNullOrWhiteSpace(AnthropicApiKey) ? null : AnthropicApiKey,
                GitHubToken = string.IsNullOrWhiteSpace(GitHubToken) ? null : GitHubToken,
                OllamaBaseUrl = OllamaBaseUrl,
                LmStudioBaseUrl = LmStudioBaseUrl,
                InsightsExecutor = string.IsNullOrWhiteSpace(InsightsExecutor) ? null : InsightsExecutor,
                InsightsModel = string.IsNullOrWhiteSpace(InsightsModel) ? null : InsightsModel,
            });

            Saved = true;
            return Task.CompletedTask;
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            return Task.CompletedTask;
        }
        finally
        {
            IsSaving = false;
        }
    }

    public string SaveNote =>
        "Settings are persisted to studio-settings.json. Restart to ensure all components pick up updates.";
}
