using AiDev.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AiDev.Desktop.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly StudioSettingsService _settingsService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _anthropicApiKey = "";
    [ObservableProperty] private string _ollamaBaseUrl = "";
    [ObservableProperty] private string _lmStudioBaseUrl = "";
    [ObservableProperty] private string _gitHubToken = "";
    [ObservableProperty] private string _insightsExecutor = "";
    [ObservableProperty] private string _insightsModel = "";

    public SettingsViewModel(StudioSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var settings = await Task.Run(() => _settingsService.GetSettings());
            AnthropicApiKey = settings.AnthropicApiKey ?? "";
            OllamaBaseUrl = settings.OllamaBaseUrl;
            LmStudioBaseUrl = settings.LmStudioBaseUrl;
            GitHubToken = settings.GitHubToken ?? "";
            InsightsExecutor = settings.InsightsExecutor ?? "";
            InsightsModel = settings.InsightsModel ?? "";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Studio settings are read from appsettings.json; runtime saving is not supported.
    /// Expose this as a note to the user.
    /// </summary>
    public string SaveNote =>
        "Settings are read from appsettings.json. Edit that file and restart to apply changes.";
}
